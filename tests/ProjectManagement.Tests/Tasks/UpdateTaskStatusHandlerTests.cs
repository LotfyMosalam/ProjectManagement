using ProjectManagement.Application.Features.Tasks.Commands.UpdateTaskStatus;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Tasks;

public class UpdateTaskStatusHandlerTests
{
    private readonly Mock<ITaskRepository>                              _mockTaskRepo    = new();
    private readonly Mock<IUnitOfWork>                                  _mockUnitOfWork  = new();
    private readonly Mock<ICurrentUserService>                          _mockCurrentUser = new();
    private readonly Mock<ICacheService>                                _mockCache       = new();
    private readonly Mock<ILogger<UpdateTaskStatusCommandHandler>>      _mockLogger      = new();

    private UpdateTaskStatusCommandHandler CreateHandler() =>
        new(_mockTaskRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object, _mockLogger.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_UpdatesStatusAndReturnsSuccess()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        var command = new UpdateTaskStatusCommand(task.Id, TaskStatus.InProgress);
        SetupUser(userId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockCache
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be("Task status updated successfully.");
        task.Status.Should().Be(TaskStatus.InProgress);

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_InvalidatesProjectTasksCacheKey()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        var command = new UpdateTaskStatusCommand(task.Id, TaskStatus.InProgress);
        SetupUser(userId);

        _mockTaskRepo.Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            x => x.RemoveAsync($"tasks_project_{task.ProjectId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── valid transitions ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.ToDo,        TaskStatus.InProgress)]
    [InlineData(TaskStatus.InProgress,  TaskStatus.Done)]
    [InlineData(TaskStatus.ToDo,        TaskStatus.Done)]
    [InlineData(TaskStatus.Done,        TaskStatus.Done)]   // Done → Done is allowed (no-op)
    public async Task Handle_WithAllowedTransition_Succeeds(TaskStatus from, TaskStatus to)
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        SetupUser(userId);

        // Advance task to the desired starting status
        if (from != TaskStatus.ToDo)
            task.UpdateStatus(from);

        var command = new UpdateTaskStatusCommand(task.Id, to);

        _mockTaskRepo.Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        task.Status.Should().Be(to);
    }

    // ── Done is a terminal state ──────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.ToDo)]
    [InlineData(TaskStatus.InProgress)]
    public async Task Handle_WhenTaskIsDoneAndStatusChanges_ThrowsInvalidOperationException(TaskStatus attemptedStatus)
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        SetupUser(userId);

        // Move task to Done first
        task.UpdateStatus(TaskStatus.Done);

        var command = new UpdateTaskStatusCommand(task.Id, attemptedStatus);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert — domain throws; handler must not swallow it
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Done*");

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── guard: task not found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new UpdateTaskStatusCommand(Guid.NewGuid(), TaskStatus.InProgress);
        SetupUser(userId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Task*");
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── guard: not owner ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId      = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var project          = DomainFactory.CreateProject(userId: ownerUserId);
        var task             = DomainFactory.CreateTaskWithProject(project);
        var command          = new UpdateTaskStatusCommand(task.Id, TaskStatus.InProgress);
        SetupUser(requestingUserId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

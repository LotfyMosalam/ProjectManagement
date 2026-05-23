using ProjectManagement.Application.Features.Tasks.Commands.DeleteTask;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Tasks;

public class DeleteTaskHandlerTests
{
    private readonly Mock<ITaskRepository>     _mockTaskRepo    = new();
    private readonly Mock<IUnitOfWork>         _mockUnitOfWork  = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private DeleteTaskCommandHandler CreateHandler() =>
        new(_mockTaskRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_DeletesTaskAndReturnsSuccess()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        var command = new DeleteTaskCommand(task.Id);
        SetupUser(userId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _mockTaskRepo
            .Setup(x => x.Delete(task));

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
        result.Data.Should().Be("Task deleted successfully.");

        _mockTaskRepo.Verify(x => x.Delete(task), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_InvalidatesProjectTasksCacheKey()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        var command = new DeleteTaskCommand(task.Id);
        SetupUser(userId);

        _mockTaskRepo.Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _mockTaskRepo.Setup(x => x.Delete(task));
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert — cache key is based on the task's ProjectId captured before deletion
        _mockCache.Verify(
            x => x.RemoveAsync($"tasks_project_{task.ProjectId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── guard: task not found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new DeleteTaskCommand(Guid.NewGuid());
        SetupUser(userId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(command.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Task*");
        _mockTaskRepo.Verify(x => x.Delete(It.IsAny<TaskItem>()), Times.Never);
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
        var command          = new DeleteTaskCommand(task.Id);
        SetupUser(requestingUserId);

        _mockTaskRepo
            .Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockTaskRepo.Verify(x => x.Delete(It.IsAny<TaskItem>()), Times.Never);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── cache key uses ProjectId captured before deletion ─────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_UsesCapturedProjectIdForCacheInvalidation()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var task    = DomainFactory.CreateTaskWithProject(project);
        var capturedProjectId = task.ProjectId;   // snapshot before anything happens
        var command = new DeleteTaskCommand(task.Id);
        SetupUser(userId);

        _mockTaskRepo.Setup(x => x.GetByIdWithProjectAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _mockTaskRepo.Setup(x => x.Delete(task));
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert — correct key with the task's ProjectId
        _mockCache.Verify(
            x => x.RemoveAsync($"tasks_project_{capturedProjectId}", It.IsAny<CancellationToken>()),
            Times.Once);

        // No other RemoveAsync calls
        _mockCache.Verify(
            x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

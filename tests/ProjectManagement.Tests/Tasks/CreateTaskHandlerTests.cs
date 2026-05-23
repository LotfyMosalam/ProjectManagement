using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Tasks.Commands.CreateTask;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Tasks;

public class CreateTaskHandlerTests
{
    private readonly Mock<IProjectRepository>                     _mockProjectRepo = new();
    private readonly Mock<IUnitOfWork>                            _mockUnitOfWork  = new();
    private readonly Mock<ICurrentUserService>                    _mockCurrentUser = new();
    private readonly Mock<ICacheService>                          _mockCache       = new();
    private readonly Mock<ILogger<CreateTaskCommandHandler>>      _mockLogger      = new();

    private CreateTaskCommandHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object, _mockLogger.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_CreatesTaskUnderProject()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new CreateTaskCommand(project.Id, "New Task", "Description", TaskPriority.High, null);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

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
        result.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("New Task");
        result.Data.ProjectId.Should().Be(project.Id);

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_InvalidatesTasksCacheKey()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new CreateTaskCommand(project.Id, "Task X", null, TaskPriority.Low, null);
        SetupUser(userId);

        _mockProjectRepo.Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            x => x.RemoveAsync($"tasks_project_{project.Id}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_AddsTaskToProjectCollection()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new CreateTaskCommand(project.Id, "My Task", null, TaskPriority.Medium, null);
        SetupUser(userId);

        _mockProjectRepo.Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert — the task was added to the project's in-memory Tasks collection
        project.Tasks.Should().HaveCount(1);
        project.Tasks.First().Title.Should().Be("My Task");
    }

    // ── guard: project not found ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new CreateTaskCommand(Guid.NewGuid(), "Task", null, TaskPriority.Low, null);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(command.ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Project*");
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── guard: not owner ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNotProjectOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId      = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var project          = DomainFactory.CreateProject(userId: ownerUserId);
        var command          = new CreateTaskCommand(project.Id, "Task", null, TaskPriority.Low, null);
        SetupUser(requestingUserId);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── guard: unauthenticated ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUser.Setup(x => x.UserId).Returns((Guid?)null);
        var command = new CreateTaskCommand(Guid.NewGuid(), "Task", null, TaskPriority.Low, null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _mockProjectRepo.Verify(
            x => x.GetByIdWithTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

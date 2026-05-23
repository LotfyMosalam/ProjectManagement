using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Tasks.Queries.GetTasksByProject;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Tasks;

public class GetTasksByProjectHandlerTests
{
    private readonly Mock<ITaskRepository>     _mockTaskRepo    = new();
    private readonly Mock<IProjectRepository>  _mockProjectRepo = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private GetTasksByProjectQueryHandler CreateHandler() =>
        new(_mockTaskRepo.Object, _mockProjectRepo.Object,
            _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── cache miss: fetches DB and populates cache ────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheMiss_FetchesFromDbAndCachesResult()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var query   = new GetTasksByProjectQuery(project.Id);
        SetupUser(userId);

        var task1 = DomainFactory.CreateTaskWithProject(project, "Task 1");
        var task2 = DomainFactory.CreateTaskWithProject(project, "Task 2");

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        _mockCache
            .Setup(x => x.GetAsync<List<TaskDto>>($"tasks_project_{project.Id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TaskDto>?)null);

        _mockTaskRepo
            .Setup(x => x.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskItem> { task1, task2 });

        _mockCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TaskDto>>(),
                                   It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);

        _mockTaskRepo.Verify(x => x.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(
            x => x.SetAsync($"tasks_project_{project.Id}", It.IsAny<List<TaskDto>>(),
                            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── cache hit: skips DB entirely ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedTasksWithoutDbCall()
    {
        // Arrange
        var userId    = Guid.NewGuid();
        var project   = DomainFactory.CreateProject(userId: userId);
        var projectId = project.Id;
        var query     = new GetTasksByProjectQuery(projectId);
        SetupUser(userId);

        // The handler checks ownership on project.UserId, then builds cache key from request.ProjectId
        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var cachedDtos = new List<TaskDto>
        {
            new(Guid.NewGuid(), projectId, "Cached Task", null,
                TaskStatus.ToDo, TaskPriority.Medium, null, DateTime.UtcNow)
        };

        _mockCache
            .Setup(x => x.GetAsync<List<TaskDto>>($"tasks_project_{projectId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDtos);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.First().Title.Should().Be("Cached Task");

        // DB must NOT be called on cache hit
        _mockTaskRepo.Verify(
            x => x.GetByProjectIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── ownership checked BEFORE cache ────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNotOwner_ThrowsForbiddenExceptionBeforeCheckingCache()
    {
        // Arrange
        var ownerUserId      = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var project          = DomainFactory.CreateProject(userId: ownerUserId);
        var query            = new GetTasksByProjectQuery(project.Id);
        SetupUser(requestingUserId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var act = () => CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        // Cache should never be checked — forbidden before that
        _mockCache.Verify(
            x => x.GetAsync<List<TaskDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── guard: project not found ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var query     = new GetTasksByProjectQuery(projectId);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act
        var act = () => CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Project*");
        _mockTaskRepo.Verify(
            x => x.GetByProjectIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── empty task list ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectHasNoTasks_ReturnsEmptyList()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var query   = new GetTasksByProjectQuery(project.Id);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        _mockCache
            .Setup(x => x.GetAsync<List<TaskDto>>($"tasks_project_{project.Id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TaskDto>?)null);

        _mockTaskRepo
            .Setup(x => x.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskItem>());

        _mockCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TaskDto>>(),
                                   It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }
}

using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Projects.Queries.GetProjectById;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Projects;

public class GetProjectByIdHandlerTests
{
    private readonly Mock<IProjectRepository>  _mockProjectRepo = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private GetProjectByIdQueryHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── cache miss + owned project ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheMissAndOwned_ReturnsProjectAndCachesIt()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var query   = new GetProjectByIdQuery(project.Id);
        SetupUser(userId);

        _mockCache
            .Setup(x => x.GetAsync<ProjectDto>($"project_{project.Id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDto?)null);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        _mockCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProjectDto>(),
                                   It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(project.Id);
        result.Data.UserId.Should().Be(userId);

        _mockCache.Verify(
            x => x.SetAsync($"project_{project.Id}", It.IsAny<ProjectDto>(),
                            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── cache hit + owned project ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheHitAndOwned_ReturnsCachedProjectWithoutDbCall()
    {
        // Arrange
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var query     = new GetProjectByIdQuery(projectId);
        SetupUser(userId);

        var cachedDto = new ProjectDto(projectId, "Cached", null, DateTime.UtcNow, userId, 0);

        _mockCache
            .Setup(x => x.GetAsync<ProjectDto>($"project_{projectId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("Cached");

        _mockProjectRepo.Verify(
            x => x.GetByIdWithTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── cache hit + not owner → ForbiddenException ─────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheHitAndNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var requestingUserId = Guid.NewGuid();
        var ownerUserId      = Guid.NewGuid();
        var projectId        = Guid.NewGuid();
        SetupUser(requestingUserId);

        var cachedDto = new ProjectDto(projectId, "Someone Else's", null, DateTime.UtcNow, ownerUserId, 0);
        _mockCache
            .Setup(x => x.GetAsync<ProjectDto>($"project_{projectId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var act = () => CreateHandler().Handle(new GetProjectByIdQuery(projectId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── project not found → NotFoundException ─────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SetupUser(userId);

        _mockCache
            .Setup(x => x.GetAsync<ProjectDto>($"project_{projectId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDto?)null);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act
        var act = () => CreateHandler().Handle(new GetProjectByIdQuery(projectId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Project*");
    }

    // ── DB hit + not owner → ForbiddenException ───────────────────────────────

    [Fact]
    public async Task Handle_WhenDbHitAndNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId      = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var project          = DomainFactory.CreateProject(userId: ownerUserId);
        SetupUser(requestingUserId);

        _mockCache
            .Setup(x => x.GetAsync<ProjectDto>($"project_{project.Id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDto?)null);

        _mockProjectRepo
            .Setup(x => x.GetByIdWithTasksAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var act = () => CreateHandler().Handle(new GetProjectByIdQuery(project.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockCache.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProjectDto>(),
                            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

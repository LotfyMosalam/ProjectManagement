using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Projects.Queries.GetAllProjects;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Projects;

public class GetAllProjectsHandlerTests
{
    private readonly Mock<IProjectRepository>  _mockProjectRepo = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private GetAllProjectsQueryHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── cache miss: fetches DB and populates cache ─────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheMiss_FetchesFromDbAndCachesResult()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var query   = new GetAllProjectsQuery(1, 10);
        SetupUser(userId);

        var projects = new List<Project>
        {
            DomainFactory.CreateProject("Project A", userId: userId),
            DomainFactory.CreateProject("Project B", userId: userId)
        };

        _mockCache
            .Setup(x => x.GetAsync<List<ProjectDto>>($"projects_user_{userId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ProjectDto>?)null);

        _mockProjectRepo
            .Setup(x => x.GetAllByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

        _mockCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<ProjectDto>>(),
                                   It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);

        _mockProjectRepo.Verify(x => x.GetAllByUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(
            x => x.SetAsync($"projects_user_{userId}", It.IsAny<List<ProjectDto>>(),
                            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── cache hit: skips DB entirely ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedResultWithoutQueryingDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query  = new GetAllProjectsQuery(1, 10);
        SetupUser(userId);

        var cachedDtos = new List<ProjectDto>
        {
            new(Guid.NewGuid(), "Cached Project", null, DateTime.UtcNow, userId, 0)
        };

        _mockCache
            .Setup(x => x.GetAsync<List<ProjectDto>>($"projects_user_{userId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDtos);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items.First().Name.Should().Be("Cached Project");

        // Repository must NOT be called on cache hit
        _mockProjectRepo.Verify(x => x.GetAllByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── pagination from cache ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query  = new GetAllProjectsQuery(PageNumber: 2, PageSize: 1);
        SetupUser(userId);

        var cachedDtos = new List<ProjectDto>
        {
            new(Guid.NewGuid(), "Project 1", null, DateTime.UtcNow, userId, 0),
            new(Guid.NewGuid(), "Project 2", null, DateTime.UtcNow, userId, 0),
        };

        _mockCache
            .Setup(x => x.GetAsync<List<ProjectDto>>($"projects_user_{userId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDtos);

        // Act
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // Assert
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items.First().Name.Should().Be("Project 2");
        result.Data.TotalCount.Should().Be(2);
        result.Data.PageNumber.Should().Be(2);
    }
}

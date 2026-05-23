using ProjectManagement.Application.Features.Projects.Commands.CreateProject;

namespace ProjectManagement.Tests.Projects;

public class CreateProjectHandlerTests
{
    private readonly Mock<IProjectRepository>                        _mockProjectRepo  = new();
    private readonly Mock<IUnitOfWork>                               _mockUnitOfWork   = new();
    private readonly Mock<ICurrentUserService>                       _mockCurrentUser  = new();
    private readonly Mock<ICacheService>                             _mockCache        = new();
    private readonly Mock<ILogger<CreateProjectCommandHandler>>      _mockLogger       = new();

    private CreateProjectCommandHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object, _mockLogger.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithAuthenticatedUser_CreatesProjectWithCorrectUserId()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new CreateProjectCommand("My Project", "A description");
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockCache
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("My Project");
        result.Data.UserId.Should().Be(userId);

        _mockProjectRepo.Verify(
            x => x.AddAsync(It.Is<Project>(p => p.UserId == userId && p.Name == "My Project"),
                            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAuthenticatedUser_InvalidatesCacheAfterSave()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new CreateProjectCommand("Project X", null);
        SetupUser(userId);

        _mockProjectRepo.Setup(x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            x => x.RemoveAsync($"projects_user_{userId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── guard: unauthenticated ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockCurrentUser.Setup(x => x.UserId).Returns((Guid?)null);
        var command = new CreateProjectCommand("Project X", null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _mockProjectRepo.Verify(x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

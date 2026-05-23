using ProjectManagement.Application.Features.Projects.Commands.UpdateProject;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Projects;

public class UpdateProjectHandlerTests
{
    private readonly Mock<IProjectRepository>  _mockProjectRepo = new();
    private readonly Mock<IUnitOfWork>         _mockUnitOfWork  = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private UpdateProjectCommandHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_UpdatesProjectAndInvalidatesCache()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new UpdateProjectCommand(project.Id, "Updated Name", "Updated Desc");
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
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
        result.Data.Should().Be("Project updated successfully.");
        project.Name.Should().Be("Updated Name");
        project.Description.Should().Be("Updated Desc");

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_InvalidatesBothCacheKeys()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new UpdateProjectCommand(project.Id, "New Name", null);
        SetupUser(userId);

        _mockProjectRepo.Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert — both keys must be invalidated
        _mockCache.Verify(x => x.RemoveAsync($"project_{project.Id}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(x => x.RemoveAsync($"projects_user_{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── guard: not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new UpdateProjectCommand(Guid.NewGuid(), "Name", null);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Project*");
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
        var command          = new UpdateProjectCommand(project.Id, "Name", null);
        SetupUser(requestingUserId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

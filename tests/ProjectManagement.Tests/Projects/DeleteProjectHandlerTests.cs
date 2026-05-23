using ProjectManagement.Application.Features.Projects.Commands.DeleteProject;
using ProjectManagement.Tests.Helpers;

namespace ProjectManagement.Tests.Projects;

public class DeleteProjectHandlerTests
{
    private readonly Mock<IProjectRepository>  _mockProjectRepo = new();
    private readonly Mock<IUnitOfWork>         _mockUnitOfWork  = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ICacheService>       _mockCache       = new();

    private DeleteProjectCommandHandler CreateHandler() =>
        new(_mockProjectRepo.Object, _mockUnitOfWork.Object,
            _mockCurrentUser.Object, _mockCache.Object);

    private void SetupUser(Guid userId) =>
        _mockCurrentUser.Setup(x => x.UserId).Returns(userId);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOwner_DeletesProjectAndReturnsSuccess()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new DeleteProjectCommand(project.Id);
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        _mockProjectRepo
            .Setup(x => x.Delete(project));

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
        result.Data.Should().Be("Project deleted successfully.");

        _mockProjectRepo.Verify(x => x.Delete(project), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidOwner_InvalidatesBothCacheKeys()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var project = DomainFactory.CreateProject(userId: userId);
        var command = new DeleteProjectCommand(project.Id);
        SetupUser(userId);

        _mockProjectRepo.Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _mockProjectRepo.Setup(x => x.Delete(project));
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(x => x.RemoveAsync($"project_{project.Id}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(x => x.RemoveAsync($"projects_user_{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── guard: not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var command = new DeleteProjectCommand(Guid.NewGuid());
        SetupUser(userId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Project*");
        _mockProjectRepo.Verify(x => x.Delete(It.IsAny<Project>()), Times.Never);
    }

    // ── guard: not owner ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerUserId      = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var project          = DomainFactory.CreateProject(userId: ownerUserId);
        var command          = new DeleteProjectCommand(project.Id);
        SetupUser(requestingUserId);

        _mockProjectRepo
            .Setup(x => x.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockProjectRepo.Verify(x => x.Delete(It.IsAny<Project>()), Times.Never);
    }
}

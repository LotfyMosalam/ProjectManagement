using ProjectManagement.Application.Features.Auth.Commands.Register;

namespace ProjectManagement.Tests.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityService>  _mockIdentityService  = new();
    private readonly Mock<IUserRepository>   _mockUserRepository   = new();
    private readonly Mock<IUnitOfWork>       _mockUnitOfWork       = new();

    private RegisterCommandHandler CreateHandler() =>
        new(_mockIdentityService.Object, _mockUserRepository.Object, _mockUnitOfWork.Object);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidRequest_CreatesUserAndReturnSuccess()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var email   = "user@example.com";
        var command = new RegisterCommand(email, "Password123!", "Password123!");

        _mockIdentityService
            .Setup(x => x.UserExistsAsync(email))
            .ReturnsAsync(false);

        _mockIdentityService
            .Setup(x => x.RegisterAsync(email, "Password123!"))
            .ReturnsAsync((true, userId.ToString(), Enumerable.Empty<string>()));

        _mockUserRepository
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNullOrEmpty();

        _mockUserRepository.Verify(
            x => x.AddAsync(It.Is<User>(u => u.Email == email), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── guard: email already exists ───────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var email   = "duplicate@example.com";
        var command = new RegisterCommand(email, "Password123!", "Password123!");

        _mockIdentityService
            .Setup(x => x.UserExistsAsync(email))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");

        _mockIdentityService.Verify(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── guard: identity service failure ───────────────────────────────────────

    [Fact]
    public async Task Handle_WhenIdentityRegistrationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var email   = "user@example.com";
        var command = new RegisterCommand(email, "weak", "weak");
        var errors  = new[] { "Password too short.", "Password requires digit." };

        _mockIdentityService
            .Setup(x => x.UserExistsAsync(email))
            .ReturnsAsync(false);

        _mockIdentityService
            .Setup(x => x.RegisterAsync(email, "weak"))
            .ReturnsAsync((false, (string?)null, errors.AsEnumerable()));

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Password too short*");

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

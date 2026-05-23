using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Auth.Commands.Login;

namespace ProjectManagement.Tests.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService>              _mockIdentityService  = new();
    private readonly Mock<IJwtTokenService>              _mockJwtTokenService  = new();
    private readonly Mock<ILogger<LoginCommandHandler>>  _mockLogger           = new();

    private LoginCommandHandler CreateHandler() =>
        new(_mockIdentityService.Object, _mockJwtTokenService.Object, _mockLogger.Object);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTokenAndUserInfo()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var email   = "user@example.com";
        var roles   = new[] { "User" };
        var command = new LoginCommand(email, "Password123!");

        _mockIdentityService
            .Setup(x => x.LoginAsync(email, "Password123!"))
            .ReturnsAsync((true, userId.ToString(), email, roles.AsEnumerable()));

        _mockJwtTokenService
            .Setup(x => x.GenerateToken(userId.ToString(), email, It.IsAny<IEnumerable<string>>()))
            .Returns(("fake-jwt-token", DateTime.UtcNow.AddHours(1)));

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().Be("fake-jwt-token");
        result.Data.Email.Should().Be(email);
        result.Data.Roles.Should().Contain("User");

        _mockJwtTokenService.Verify(
            x => x.GenerateToken(userId.ToString(), email, It.IsAny<IEnumerable<string>>()),
            Times.Once);
    }

    // ── guard: invalid credentials ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithInvalidCredentials_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var command = new LoginCommand("user@example.com", "wrong-password");

        _mockIdentityService
            .Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, (string?)null, (string?)null, Enumerable.Empty<string>()));

        // Act
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");

        _mockJwtTokenService.Verify(
            x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }
}

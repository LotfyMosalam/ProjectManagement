using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, ApiResponse<AuthResponseDto>>
{
    public async Task<ApiResponse<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Login attempt | Timestamp: {Timestamp:o} | Email: {Email}",
            DateTime.UtcNow, request.Email);

        var (succeeded, userId, email, roles) = await identityService.LoginAsync(
            request.Email, request.Password);

        if (!succeeded)
        {
            logger.LogWarning(
                "Login failed | Timestamp: {Timestamp:o} | Email: {Email} | Status: InvalidCredentials",
                DateTime.UtcNow, request.Email);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var (token, _) = jwtTokenService.GenerateToken(userId!, email!, roles);

        logger.LogInformation(
            "Login succeeded | Timestamp: {Timestamp:o} | UserId: {UserId} | Email: {Email} | Roles: {Roles} | Status: Success",
            DateTime.UtcNow, userId, email, string.Join(",", roles));

        return ApiResponse<AuthResponseDto>.Success(
            new AuthResponseDto(token, email!, roles),
            "Login successful.");
    }
}

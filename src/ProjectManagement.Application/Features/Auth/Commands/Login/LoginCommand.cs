using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Email, string Password)
    : IRequest<ApiResponse<AuthResponseDto>>;

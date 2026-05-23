using MediatR;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string ConfirmPassword
) : IRequest<ApiResponse<string>>;

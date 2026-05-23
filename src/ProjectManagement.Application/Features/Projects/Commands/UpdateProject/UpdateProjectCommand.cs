using MediatR;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.UpdateProject;

public record UpdateProjectCommand(Guid Id, string Name, string? Description)
    : IRequest<ApiResponse<string>>;

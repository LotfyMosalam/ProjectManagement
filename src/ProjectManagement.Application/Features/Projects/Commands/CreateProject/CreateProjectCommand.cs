using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.CreateProject;

public record CreateProjectCommand(string Name, string? Description)
    : IRequest<ApiResponse<ProjectDto>>;

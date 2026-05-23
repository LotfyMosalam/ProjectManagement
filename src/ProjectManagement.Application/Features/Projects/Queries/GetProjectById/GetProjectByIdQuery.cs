using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Queries.GetProjectById;

public record GetProjectByIdQuery(Guid Id) : IRequest<ApiResponse<ProjectDto>>;

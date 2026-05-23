using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Queries.GetAllProjects;

public record GetAllProjectsQuery(int PageNumber = 1, int PageSize = 10)
    : IRequest<ApiResponse<PaginatedResponse<ProjectDto>>>;

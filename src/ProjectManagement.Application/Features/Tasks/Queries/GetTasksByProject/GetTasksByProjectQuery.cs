using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Queries.GetTasksByProject;

public record GetTasksByProjectQuery(Guid ProjectId)
    : IRequest<ApiResponse<List<TaskDto>>>;

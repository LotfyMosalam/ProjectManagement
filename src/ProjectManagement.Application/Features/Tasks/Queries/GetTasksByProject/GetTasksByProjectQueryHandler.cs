using Mapster;
using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Queries.GetTasksByProject;

public sealed class GetTasksByProjectQueryHandler(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<GetTasksByProjectQuery, ApiResponse<List<TaskDto>>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<ApiResponse<List<TaskDto>>> Handle(
        GetTasksByProjectQuery request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        // Ownership is validated on the project BEFORE the cache is checked,
        // so cached task lists can never be served to non-owners.
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        // Admin can see tasks for any project; User may only see their own.
        if (!isAdmin && project.UserId != userId)
            throw new ForbiddenException();

        var cacheKey = $"tasks_project_{request.ProjectId}";

        var cached = await cacheService.GetAsync<List<TaskDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return ApiResponse<List<TaskDto>>.Success(cached);

        var tasks = await taskRepository.GetByProjectIdAsync(request.ProjectId, cancellationToken);
        var dtos  = tasks.Adapt<List<TaskDto>>();

        await cacheService.SetAsync(cacheKey, dtos, CacheTtl, cancellationToken);

        return ApiResponse<List<TaskDto>>.Success(dtos);
    }
}

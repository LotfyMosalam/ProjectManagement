using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Queries.GetAllProjects;

public sealed class GetAllProjectsQueryHandler(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<GetAllProjectsQuery, ApiResponse<PaginatedResponse<ProjectDto>>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Admin sees every project across all users; User sees only their own.
    private const string AdminCacheKey = "projects_all";

    public async Task<ApiResponse<PaginatedResponse<ProjectDto>>> Handle(
        GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var cacheKey = isAdmin ? AdminCacheKey : $"projects_user_{userId}";

        var allDtos = await cacheService.GetAsync<List<ProjectDto>>(cacheKey, cancellationToken);

        if (allDtos is null)
        {
            var projects = isAdmin
                ? await projectRepository.GetAllWithTasksAsync(cancellationToken)
                : await projectRepository.GetAllByUserAsync(userId, cancellationToken);

            allDtos = projects
                .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt, p.UserId, p.Tasks.Count))
                .ToList();

            await cacheService.SetAsync(cacheKey, allDtos, CacheTtl, cancellationToken);
        }

        var totalCount = allDtos.Count;
        var paged      = allDtos
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);

        var paginated = PaginatedResponse<ProjectDto>.Create(paged, totalCount, request.PageNumber, request.PageSize);
        return ApiResponse<PaginatedResponse<ProjectDto>>.Success(paginated);
    }
}

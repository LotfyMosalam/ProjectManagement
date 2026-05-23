using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Queries.GetProjectById;

public sealed class GetProjectByIdQueryHandler(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<GetProjectByIdQuery, ApiResponse<ProjectDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<ApiResponse<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin  = currentUserService.IsAdmin;
        var cacheKey = $"project_{request.Id}";

        // ── Cache hit ──────────────────────────────────────────────────────────
        var dto = await cacheService.GetAsync<ProjectDto>(cacheKey, cancellationToken);

        if (dto is not null)
        {
            // Admin can view any project; User may only view their own.
            if (!isAdmin && dto.UserId != userId)
                throw new ForbiddenException();

            return ApiResponse<ProjectDto>.Success(dto);
        }

        // ── Cache miss — fetch from DB ─────────────────────────────────────────
        var project = await projectRepository.GetByIdWithTasksAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Project", request.Id);

        if (!isAdmin && project.UserId != userId)
            throw new ForbiddenException();

        dto = new ProjectDto(project.Id, project.Name, project.Description,
                             project.CreatedAt, project.UserId, project.Tasks.Count);

        await cacheService.SetAsync(cacheKey, dto, CacheTtl, cancellationToken);

        return ApiResponse<ProjectDto>.Success(dto);
    }
}

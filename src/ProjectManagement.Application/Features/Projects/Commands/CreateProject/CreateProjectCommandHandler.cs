using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandHandler(
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService,
    ILogger<CreateProjectCommandHandler> logger)
    : IRequestHandler<CreateProjectCommand, ApiResponse<ProjectDto>>
{
    public async Task<ApiResponse<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var project = Project.Create(request.Name, request.Description, userId);

        await projectRepository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate the user's list cache AND the admin's all-projects cache.
        await Task.WhenAll(
            cacheService.RemoveAsync($"projects_user_{userId}", cancellationToken),
            cacheService.RemoveAsync("projects_all", cancellationToken));

        logger.LogInformation(
            "Project created | Timestamp: {Timestamp:o} | UserId: {UserId} | ProjectId: {ProjectId} | Name: {Name} | Status: Success",
            DateTime.UtcNow, userId, project.Id, project.Name);

        var dto = new ProjectDto(project.Id, project.Name, project.Description,
                                 project.CreatedAt, project.UserId, project.Tasks.Count);

        return ApiResponse<ProjectDto>.Success(dto, "Project created successfully.");
    }
}

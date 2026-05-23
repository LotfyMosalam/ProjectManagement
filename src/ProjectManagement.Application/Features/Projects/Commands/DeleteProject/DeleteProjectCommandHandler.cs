using MediatR;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.DeleteProject;

public sealed class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<DeleteProjectCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Project", request.Id);

        // Admin can delete any project; User may only delete their own.
        if (!isAdmin && project.UserId != userId)
            throw new ForbiddenException();

        // Capture the owner's id before the entity is deleted.
        var ownerId = project.UserId;

        projectRepository.Delete(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate the owner's per-user list, the individual project entry,
        // and the admin all-projects cache.
        await Task.WhenAll(
            cacheService.RemoveAsync($"project_{request.Id}", cancellationToken),
            cacheService.RemoveAsync($"projects_user_{ownerId}", cancellationToken),
            cacheService.RemoveAsync("projects_all", cancellationToken));

        return ApiResponse<string>.Success("Project deleted successfully.");
    }
}

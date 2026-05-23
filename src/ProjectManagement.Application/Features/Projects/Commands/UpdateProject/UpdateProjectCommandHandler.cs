using MediatR;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.UpdateProject;

public sealed class UpdateProjectCommandHandler(
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<UpdateProjectCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Project", request.Id);

        // Admin can update any project; User may only update their own.
        if (!isAdmin && project.UserId != userId)
            throw new ForbiddenException();

        project.Update(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Use project.UserId (not current userId) so an admin updating another
        // user's project still invalidates the correct per-user cache entry.
        await Task.WhenAll(
            cacheService.RemoveAsync($"project_{request.Id}", cancellationToken),
            cacheService.RemoveAsync($"projects_user_{project.UserId}", cancellationToken),
            cacheService.RemoveAsync("projects_all", cancellationToken));

        return ApiResponse<string>.Success("Project updated successfully.");
    }
}

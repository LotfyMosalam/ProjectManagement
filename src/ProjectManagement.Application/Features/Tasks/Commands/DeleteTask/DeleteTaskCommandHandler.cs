using MediatR;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Commands.DeleteTask;

public sealed class DeleteTaskCommandHandler(
    ITaskRepository taskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService)
    : IRequestHandler<DeleteTaskCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var task = await taskRepository.GetByIdWithProjectAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Task", request.TaskId);

        // Admin can delete any task; User may only delete tasks in their own projects.
        if (!isAdmin && task.Project.UserId != userId)
            throw new ForbiddenException();

        var projectId = task.ProjectId;   // capture before deletion

        taskRepository.Delete(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveAsync($"tasks_project_{projectId}", cancellationToken);

        return ApiResponse<string>.Success("Task deleted successfully.");
    }
}

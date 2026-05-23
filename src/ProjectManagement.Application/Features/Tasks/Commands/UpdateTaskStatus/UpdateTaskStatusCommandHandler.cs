using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Commands.UpdateTaskStatus;

public sealed class UpdateTaskStatusCommandHandler(
    ITaskRepository taskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService,
    ILogger<UpdateTaskStatusCommandHandler> logger)
    : IRequestHandler<UpdateTaskStatusCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var task = await taskRepository.GetByIdWithProjectAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Task", request.TaskId);

        // Admin can update status on any task; User may only update tasks in their own projects.
        if (!isAdmin && task.Project.UserId != userId)
            throw new ForbiddenException();

        var previousStatus = task.Status;
        task.UpdateStatus(request.Status);   // may throw InvalidOperationException for Done → other

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cacheService.RemoveAsync($"tasks_project_{task.ProjectId}", cancellationToken);

        logger.LogInformation(
            "Task status updated | Timestamp: {Timestamp:o} | UserId: {UserId} | TaskId: {TaskId} | From: {From} | To: {To} | Status: Success",
            DateTime.UtcNow, userId, task.Id, previousStatus, task.Status);

        return ApiResponse<string>.Success("Task status updated successfully.");
    }
}

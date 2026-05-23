using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Commands.CreateTask;

public sealed class CreateTaskCommandHandler(
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICacheService cacheService,
    ILogger<CreateTaskCommandHandler> logger)
    : IRequestHandler<CreateTaskCommand, ApiResponse<TaskDto>>
{
    public async Task<ApiResponse<TaskDto>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var userId  = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdmin = currentUserService.IsAdmin;

        var project = await projectRepository.GetByIdWithTasksAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        // Admin can add tasks to any project; User may only add to their own.
        if (!isAdmin && project.UserId != userId)
            throw new ForbiddenException();

        var task = project.AddTask(request.Title, request.Description, request.Priority, request.DueDate);

        // Explicitly register the new entity with the change tracker (Added state).
        // project.AddTask adds the task to the _tasks collection, but EF Core's
        // snapshot-based detection on a private List<T> backing field is unreliable —
        // the entity must be explicitly tracked, same as CreateProjectCommandHandler does
        // with projectRepository.AddAsync.
        await taskRepository.AddAsync(task, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveAsync($"tasks_project_{request.ProjectId}", cancellationToken);

        logger.LogInformation(
            "Task created | Timestamp: {Timestamp:o} | UserId: {UserId} | TaskId: {TaskId} | ProjectId: {ProjectId} | Title: {Title} | Status: Success",
            DateTime.UtcNow, userId, task.Id, task.ProjectId, task.Title);

        return ApiResponse<TaskDto>.Success(task.Adapt<TaskDto>(), "Task created successfully.");
    }
}

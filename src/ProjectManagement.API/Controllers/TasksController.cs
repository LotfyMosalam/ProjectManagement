using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Tasks.Commands.CreateTask;
using ProjectManagement.Application.Features.Tasks.Commands.DeleteTask;
using ProjectManagement.Application.Features.Tasks.Commands.UpdateTaskStatus;
using ProjectManagement.Application.Features.Tasks.Queries.GetTasksByProject;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/projects/{projectId:guid}/tasks")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class TasksController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Returns all tasks for the given project.
    /// Admin can list tasks for any project; User can only list tasks for their own projects.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(ApiResponse<List<TaskDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTasksByProjectQuery(projectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new task under the given project.
    /// Only Users (not Admins) may create tasks.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "User")]
    [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { ProjectId = projectId }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Updates the status of a task.
    /// Only Users (not Admins) may change task status.
    /// Done is a terminal state — tasks cannot be moved out of Done.
    /// </summary>
    [HttpPatch("{taskId:guid}/status")]
    [Authorize(Roles = "User")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid taskId,
        [FromBody] UpdateTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { TaskId = taskId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes a task.
    /// Admin can delete any task; User can only delete tasks in their own projects.
    /// </summary>
    [HttpDelete("{taskId:guid}")]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteTaskCommand(taskId), cancellationToken);
        return Ok(result);
    }
}

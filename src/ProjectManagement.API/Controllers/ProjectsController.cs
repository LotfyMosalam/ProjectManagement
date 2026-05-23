using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Application.Features.Projects.Commands.CreateProject;
using ProjectManagement.Application.Features.Projects.Commands.DeleteProject;
using ProjectManagement.Application.Features.Projects.Commands.UpdateProject;
using ProjectManagement.Application.Features.Projects.Queries.GetAllProjects;
using ProjectManagement.Application.Features.Projects.Queries.GetProjectById;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class ProjectsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of projects.
    /// Admin receives all projects; User receives only their own.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ProjectDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAllProjectsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single project by ID.
    /// Admin can access any project; User can only access their own.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProjectByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new project for the authenticated user.
    /// Only Users (not Admins) may create projects.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "User")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Updates project name / description.
    /// Admin can update any project; User can only update their own.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes a project and all its tasks.
    /// Only Admins may delete projects.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProjectCommand(id), cancellationToken);
        return Ok(result);
    }
}

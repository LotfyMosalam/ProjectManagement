using MediatR;
using ProjectManagement.Application.DTOs;
using ProjectManagement.Domain.Enums;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Commands.CreateTask;

public record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTime? DueDate
) : IRequest<ApiResponse<TaskDto>>;

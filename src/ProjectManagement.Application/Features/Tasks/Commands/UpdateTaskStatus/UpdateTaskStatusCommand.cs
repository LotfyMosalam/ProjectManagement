using MediatR;
using ProjectManagement.Shared.Responses;
using TaskStatus = ProjectManagement.Domain.Enums.TaskStatus;

namespace ProjectManagement.Application.Features.Tasks.Commands.UpdateTaskStatus;

public record UpdateTaskStatusCommand(Guid TaskId, TaskStatus Status)
    : IRequest<ApiResponse<string>>;

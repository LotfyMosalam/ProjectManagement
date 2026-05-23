using MediatR;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Tasks.Commands.DeleteTask;

public record DeleteTaskCommand(Guid TaskId) : IRequest<ApiResponse<string>>;

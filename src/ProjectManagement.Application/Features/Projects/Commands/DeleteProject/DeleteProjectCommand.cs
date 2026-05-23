using MediatR;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Projects.Commands.DeleteProject;

public record DeleteProjectCommand(Guid Id) : IRequest<ApiResponse<string>>;

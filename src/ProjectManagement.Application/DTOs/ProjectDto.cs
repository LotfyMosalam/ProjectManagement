namespace ProjectManagement.Application.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    Guid UserId,
    int TaskCount
);

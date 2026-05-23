namespace ProjectManagement.Application.DTOs;

public record AuthResponseDto(
    string Token,
    string Email,
    IEnumerable<string> Roles
);

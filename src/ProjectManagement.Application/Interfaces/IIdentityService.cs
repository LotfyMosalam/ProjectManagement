namespace ProjectManagement.Application.Interfaces;

public interface IIdentityService
{
    Task<bool> UserExistsAsync(string email);

    Task<(bool Succeeded, string? UserId, IEnumerable<string> Errors)> RegisterAsync(
        string email, string password);

    Task<(bool Succeeded, string? UserId, string? Email, IEnumerable<string> Roles)> LoginAsync(
        string email, string password);
}

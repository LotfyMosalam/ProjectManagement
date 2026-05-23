using Microsoft.AspNetCore.Identity;
using ProjectManagement.Application.Interfaces;

namespace ProjectManagement.Infrastructure.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    public async Task<bool> UserExistsAsync(string email) =>
        await userManager.FindByEmailAsync(email) is not null;

    public async Task<(bool Succeeded, string? UserId, IEnumerable<string> Errors)> RegisterAsync(
        string email, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            return (false, null, result.Errors.Select(e => e.Description));

        await userManager.AddToRoleAsync(user, "User");

        return (true, user.Id, []);
    }

    public async Task<(bool Succeeded, string? UserId, string? Email, IEnumerable<string> Roles)> LoginAsync(
        string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !await userManager.CheckPasswordAsync(user, password))
            return (false, null, null, []);

        var roles = await userManager.GetRolesAsync(user);
        return (true, user.Id, user.Email, roles);
    }
}

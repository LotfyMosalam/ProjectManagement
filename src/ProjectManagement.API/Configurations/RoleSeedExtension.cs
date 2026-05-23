using Microsoft.AspNetCore.Identity;
using ProjectManagement.Infrastructure.Identity;

namespace ProjectManagement.API.Configurations;

public static class RoleSeedExtension
{
    private static readonly string[] Roles = ["Admin", "User"];

    public static async Task SeedRolesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

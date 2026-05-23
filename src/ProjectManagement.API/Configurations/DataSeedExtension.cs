using Microsoft.AspNetCore.Identity;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Enums;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Identity;

namespace ProjectManagement.API.Configurations;

/// <summary>
/// Seeds default users on application startup.
/// Roles must already exist (SeedRolesAsync runs first).
/// Creates BOTH an Identity user (AspNetUsers) AND a matching domain User entity (Users)
/// sharing the same Guid Id — required because Projects.UserId FK references Users.Id.
/// Safe to call on every startup (idempotent).
/// </summary>
public static class DataSeedExtension
{
    private record SeedUser(string Email, string Password, string Role);

    private static readonly SeedUser[] DefaultUsers =
    [
        new("admin@pm.com", "Admin123!", "Admin"),
        new("user@pm.com",  "User123!",  "User")
    ];

    public static async Task SeedDefaultUsersAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var userManager    = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork     = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger         = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        foreach (var seed in DefaultUsers)
        {
            var domainRole = seed.Role == "Admin" ? UserRole.Admin : UserRole.User;

            // ── 1. Identity user ──────────────────────────────────────────────
            var appUser = await userManager.FindByEmailAsync(seed.Email);

            if (appUser is null)
            {
                appUser = new ApplicationUser
                {
                    UserName  = seed.Email,
                    Email     = seed.Email,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(appUser, seed.Password);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(appUser, seed.Role);
                    logger.LogInformation(
                        "Seed user created — Email: {Email}, Role: {Role}", seed.Email, seed.Role);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError(
                        "Failed to create seed user {Email}: {Errors}", seed.Email, errors);
                    continue; // skip domain user — nothing to link to
                }
            }
            else
            {
                logger.LogDebug("Identity user {Email} already exists — skipping Identity creation", seed.Email);
            }

            // ── 2. Domain User entity (same Guid, required for FK on Projects) ─
            var userId = Guid.Parse(appUser.Id);
            var existingDomainUser = await userRepository.GetByEmailAsync(seed.Email);

            if (existingDomainUser is null)
            {
                var domainUser = User.Create(userId, seed.Email, domainRole);
                await userRepository.AddAsync(domainUser, CancellationToken.None);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                logger.LogInformation(
                    "Domain User record created — Email: {Email}, Id: {UserId}", seed.Email, userId);
            }
            else
            {
                logger.LogDebug("Domain User {Email} already exists — skipping", seed.Email);
            }
        }
    }
}

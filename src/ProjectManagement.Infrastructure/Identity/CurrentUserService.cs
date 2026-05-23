using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Infrastructure.Identity;

/// <summary>
/// Resolves the current user's identity from the JWT claims in the HTTP context.
/// Implements Domain's ICurrentUserService — no Identity framework leak into Domain.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return value is not null && Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin =>
        httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
}

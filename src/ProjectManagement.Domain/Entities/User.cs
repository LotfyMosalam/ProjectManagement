using ProjectManagement.Domain.Common;
using ProjectManagement.Domain.Enums;

namespace ProjectManagement.Domain.Entities;

/// <summary>
/// Pure domain User entity — no dependency on ASP.NET Identity or EF Core.
/// The Id is shared with ApplicationUser (Infrastructure) so authentication
/// and business data stay in sync via the same Guid key.
/// </summary>
public sealed class User : AuditableEntity
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public UserRole Role { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    private readonly List<Project> _projects = [];
    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    private User() { }

    /// <param name="id">Must equal the ApplicationUser.Id so both records share the same Guid key.</param>
    public static User Create(Guid id, string firstName, string lastName, string email, UserRole role = UserRole.User)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName ?? string.Empty,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Registration overload — derives display name from email local part.</summary>
    public static User Create(Guid id, string email, UserRole role = UserRole.User)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User
        {
            Id = id,
            FirstName = email.Split('@')[0],
            LastName = string.Empty,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(UserRole role)
    {
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }
}

namespace ProjectManagement.Domain.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>True when the JWT contains a ClaimTypes.Role of "Admin".</summary>
    bool IsAdmin { get; }
}

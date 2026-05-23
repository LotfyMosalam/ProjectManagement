namespace ProjectManagement.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(string userId, string email, IEnumerable<string> roles);
}

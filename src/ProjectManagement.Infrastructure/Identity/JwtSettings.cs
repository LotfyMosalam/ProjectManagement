namespace ProjectManagement.Infrastructure.Identity;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public string Secret { get; init; } = default!;
    public int ExpiryInMinutes { get; init; } = 60;
}

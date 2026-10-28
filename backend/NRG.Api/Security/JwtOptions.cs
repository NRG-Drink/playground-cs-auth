namespace NRG.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public double LifetimeMinutes { get; init; } = 60;
    public double RefreshTokenLifetimeDays { get; init; } = 7;
}

public sealed record DemoLoginRequest(string Username, string Password);
public sealed record DemoTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NRG.Api.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NRG.Api.Security;

public record LoginInput(string Username, string Password);
public record TokenResponse(string Token, DateTimeOffset ExpiresAt);

public class TokenService(NotesDbContext db, JwtOptions jwtOptions) //: ITokenService
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string CookiePath = "/auth"; // only sent to this endpoint

    public static string HashPassword(string password)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var passwordHashBytes = SHA256.HashData(passwordBytes);
        return Convert.ToBase64String(passwordHashBytes);
    }

    public string? GetRefreshTokenCookie(HttpContext http)
        => http.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var rt)
            ? rt
            : null;

    public void SetRefreshTokenCookie(HttpContext http, TokenResponse tokenResponse)
    {
        var options = GetRefreshTokenCookieOptions(tokenResponse.ExpiresAt.UtcDateTime);
        http.Response.Cookies.Append(RefreshTokenCookieName, tokenResponse.Token, options);
    }

    public void DeleteRefreshTokenCookie(HttpContext http)
    {
        http.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = CookiePath });
    }

    public string GetIpAddress(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public async Task<(TokenResponse Token, User User)> RotateRefreshTokenOrThrow(HttpContext httpContext)
    {
        var ip = GetIpAddress(httpContext);
        var oldRefreshToken = GetRefreshTokenCookie(httpContext)
            ?? throw new UnauthorizedAccessException("Refresh token not found.");
        var oldTokenHash = HashToken(oldRefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == oldTokenHash)
            // Unknown token → forged/garbage → plain rejection
            ?? throw new UnauthorizedAccessException("Refresh token not found in database.");

        // Check if the old token has been revoked (indicating reuse)
        if (stored.Revoked is not null)
        {
            await RevokeTokenFamily(stored.FamilyId, ip);
            DeleteRefreshTokenCookie(httpContext);
            throw new UnauthorizedAccessException("Refresh token reuse detected. All tokens in this family have been revoked.");
        }

        if (stored.IsExpired)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == stored.UserId)
            ?? throw new UnauthorizedAccessException("User not found for the provided refresh token.");

        var refreshTokenResponse = await GenerateRefreshTokenAsync(user, ip, stored);
        return (refreshTokenResponse, user);
    }

    public async Task RevokeTokenFamily(Guid familyId, string ip, string reason = "reuse-detected")
    {
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.Revoked == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Revoked, DateTime.UtcNow)
                .SetProperty(t => t.RevokedByIp, ip)
                .SetProperty(t => t.ReasonRevoked, reason));

        await db.SaveChangesAsync();
    }

    public static string HashTokenStatic(string rawToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var tokenHashBytes = SHA256.HashData(tokenBytes);
        var tokenHashHex = Convert.ToHexString(tokenHashBytes);
        return tokenHashHex;
    }

    public string HashToken(string rawToken)
        => HashTokenStatic(rawToken);

    public CookieOptions GetRefreshTokenCookieOptions(DateTime expiresAt)
        => new()
        {
            HttpOnly = true,                          // invisible to JS (XSS protection)
            Secure = true,                            // HTTPS only
            SameSite = SameSiteMode.Strict,           // CSRF protection
            Expires = expiresAt,
            Path = CookiePath                         // scoped!
        };

    public TokenResponse GenerateAccessToken(User dbUser)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dbUser.UserId),
            new Claim(ClaimTypes.Email, dbUser.Username),
            new Claim(ClaimTypes.Role, dbUser.Role),
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new TokenResponse(accessToken, expiresAt);
    }

    public async Task<TokenResponse> GenerateRefreshTokenAsync(User user, string ip, RefreshToken? stored)
    {
        var newRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var newRefreshToken = new RefreshToken
        {
            TokenHash = HashToken(newRawToken),
            FamilyId = stored?.FamilyId ?? Guid.NewGuid(),          // same family!
            UserId = stored?.UserId ?? user.UserId,
            Created = DateTime.UtcNow,
            CreatedByIp = ip,
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.RefreshTokenLifetimeDays),
            ParentTokenId = stored?.Id
        };

        stored?.Revoked = DateTime.UtcNow;
        stored?.RevokedByIp = ip;
        stored?.ReasonRevoked = "rotated";

        var entity = await db.RefreshTokens.AddAsync(newRefreshToken);
        await db.SaveChangesAsync();

        return new(newRawToken, entity.Entity.ExpiresAt);
    }
}

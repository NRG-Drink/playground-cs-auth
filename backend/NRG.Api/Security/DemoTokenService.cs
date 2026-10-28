using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NRG.Api.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NRG.Api.Security;

public class SeedAppSettingsUsers(IConfiguration configuration, NotesDbContext db)
{
    public async Task SeedIdempotentAsync()
    {
        var users = configuration
            .GetSection("DemoUsers")
            .Get<Dictionary<string, DemoTokenService.DemoUser>>()
            ?? throw new InvalidOperationException("DemoUsers configuration is required.");

        foreach (var kvp in users)
        {
            var user = kvp.Value;
            var existingUser = await db.Users.AnyAsync(e => e.UserId == user.UserId);
            if (!existingUser)
            {
                var passwordHash = TokenService.HashPassword(user.Password);
                var dbUser = new User()
                {
                    UserId = user.UserId,
                    Username = kvp.Key,
                    Role = user.Role,
                    PasswordHash = passwordHash
                };
                db.Users.Add(dbUser);
            }
        }

        await db.SaveChangesAsync();
    }
}

public sealed class DemoTokenService(IConfiguration configuration, JwtOptions options)
{
    public DemoTokenResponse? Issue(DemoLoginRequest request)
    {
        var users = configuration
            .GetSection("DemoUsers")
            .Get<Dictionary<string, DemoUser>>()
            ?? throw new InvalidOperationException("DemoUsers configuration is required.");

        var user = users.TryGetValue(request.Username, out var foundUser) ? foundUser : null;
        if (user is null)
        {
            return null;
        }

        var userPasswordBytes = Encoding.UTF8.GetBytes(user.Password);
        var requestPasswordBytes = Encoding.UTF8.GetBytes(request.Password);
        var isPasswordMatch = CryptographicOperations.FixedTimeEquals(userPasswordBytes, requestPasswordBytes);
        if (!isPasswordMatch)
        {
            return null;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),  
            //new Claim(ClaimTypes.Email, user.Email),  
            new Claim(ClaimTypes.Email, request.Username),  
            new Claim(ClaimTypes.Role, user.Role),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new DemoTokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    internal object? Refresh(NotesDbContext db, HttpContext context)
    {
        throw new NotImplementedException();
    }

    public sealed class DemoUser
    {
        public required string UserId { get; init; }
        public required string Password { get; init; }
        public required string Role { get; init; }
    }
}

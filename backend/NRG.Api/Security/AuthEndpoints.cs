using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using NRG.Api.Data;
using System.Security.Cryptography;
using System.Text;

namespace NRG.Api.Security;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        var login = new LoginEndpoint();
        builder.MapPost("/login", login.HandleLogin);

        var refresh = new RefreshEndpoint();
        builder.MapPost("/refresh", refresh.HandleRefresh);

        var logout = new LogoutEndpoint();
        builder.MapPost("/logout", logout.HandleLogout);

        return builder;
    }
}

public class LogoutEndpoint
{
    public async Task<IResult> HandleLogout(
        HttpContext httpContext,
        [Service] TokenService tokenService,
        [Service] NotesDbContext db)
    {
        try
        {
            var refreshToken = tokenService.GetRefreshTokenCookie(httpContext);
            if (refreshToken is not null)
            {
                var ip = tokenService.GetIpAddress(httpContext);
                var oldTokenHash = tokenService.HashToken(refreshToken);
                var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == oldTokenHash);
                if (stored is not null)
                {
                    await tokenService.RevokeTokenFamily(stored.FamilyId, ip, "logout");
                }
            }

            tokenService.DeleteRefreshTokenCookie(httpContext);

            return Results.Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            // Don't expose internal errors to the client in production.
            // Log the exception and return a generic error response.
            return Results.InternalServerError(ex);
        }
    }
}

public class RefreshEndpoint
{
    public async Task<IResult> HandleRefresh(
        HttpContext httpContext,
        [Service] TokenService tokenService)
    {
        try
        {
            var (rotatedRefreshToken, user) = await tokenService.RotateRefreshTokenOrThrow(httpContext);
            var accessToken = tokenService.GenerateAccessToken(user);

            var cookieOptions = tokenService.GetRefreshTokenCookieOptions(rotatedRefreshToken.ExpiresAt.DateTime);
            tokenService.SetRefreshTokenCookie(httpContext, rotatedRefreshToken);

            return Results.Ok(accessToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            // Don't expose internal errors to the client in production.
            // Log the exception and return a generic error response.
            return Results.InternalServerError(ex);
        }
    }
}

public class LoginEndpoint
{
    public async Task<IResult> HandleLogin(
        LoginInput input,
        HttpContext httpContext,
        [Service] TokenService tokenService,
        [Service] NotesDbContext db)
    {
        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Username == input.Username);

        var inputPasswordHash = TokenService.HashPassword(input.Password);
        var isPasswordValid = IsPasswordValid(inputPasswordHash, dbUser?.PasswordHash);
        if (!isPasswordValid)
        {
            return Results.Unauthorized();
        }

        var ipAddress = tokenService.GetIpAddress(httpContext);
        var accessToken = tokenService.GenerateAccessToken(dbUser!);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(dbUser!, ipAddress, null);
        var cookieOptions = tokenService.GetRefreshTokenCookieOptions(refreshToken.ExpiresAt.DateTime);
        tokenService.SetRefreshTokenCookie(httpContext, refreshToken);

        return Results.Ok(accessToken);
    }

    private static bool IsPasswordValid(string? pwHash1, string? pwHash2)
    {
        if (pwHash1 is null || pwHash2 is null)
        {
            return false;
        }

        var pw1 = Encoding.UTF8.GetBytes(pwHash1);
        var pw2 = Encoding.UTF8.GetBytes(pwHash2);
        var isPasswordMatch = CryptographicOperations.FixedTimeEquals(pw2, pw1);

        return isPasswordMatch;
    }
}

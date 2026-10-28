using Microsoft.EntityFrameworkCore;
using NRG.Api.Security;
using System.Net;

namespace NRG.Api.Tests.AuthEndpoints.Expired;

public class ExpiredTestFactory : AuthApiFactory
{
    override public string ContentRootPath => Path.Combine(RootPath, "Expired");
}

[Category("Auth")]
[ClassDataSource<AuthApiTestFixture<ExpiredTestFactory>>(Shared = SharedType.PerClass)]
public sealed class AuthEndpointTestsExpired(AuthApiTestFixture<ExpiredTestFactory> fixture) : AuthEndpointsTestsBase<ExpiredTestFactory>(fixture)
{
    [Test]
    public async Task Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie()
    {
        await TestIsNotAuthorized();

        using var response = await LoginAsync("test", "test");
        var (_, _, accessToken) = await TestAndAddRtandAtAsync(response, isLogin: true);

        await TestIsAuthorized(accessToken);
    }

    [Test, DependsOn(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie))]
    public async Task Access_WithExpiredToken_ShouldFail()
    {
        var (_, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));
        await TestIsAuthorized(accessToken);

        await Task.Delay(4_000);
        await TestIsNotAuthorized(accessToken);
    }

    [Test, DependsOn(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie))]
    public async Task Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie()
    {
        var (_, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        await TestIsAuthorized(accessToken!);

        using var refreshResponse = await RefreshToken(refreshCookie);
        var (_, _, newAtToken) = await TestAndAddRtandAtAsync(refreshResponse);

        await TestIsAuthorized(newAtToken);
    }

    [Test, DependsOn(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie))]
    public async Task Refresh_WithExpiredRT_ShouldFail()
    {
        var (refreshToken, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        await Task.Delay(4_000);
        using var refreshResponse = await RefreshToken(refreshCookie);
        await Assert.That(refreshResponse.StatusCode)
            .IsEqualTo(HttpStatusCode.Unauthorized);

        var refreshHash = TokenService.HashTokenStatic(refreshToken);
        using var db = fixture.Factory.DbFactory();
        var dbRefreshToken = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == refreshHash);
        var familiTokens = await db.RefreshTokens.Where(e => e.FamilyId == dbRefreshToken.FamilyId).ToListAsync();
        await Assert.That(familiTokens)
            .ContainsOnly(e => e.IsExpired);
    }
}

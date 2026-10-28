using System.Net;

namespace NRG.Api.Tests.AuthEndpoints.Logout;

public class LogoutTestFactory : AuthApiFactory
{
    override public string ContentRootPath => Path.Combine(RootPath, "Logout");
}

[Category("Auth")]
[ClassDataSource<AuthApiTestFixture<LogoutTestFactory>>(Shared = SharedType.PerClass)]
public sealed class AuthEndpointTestsLogout(AuthApiTestFixture<LogoutTestFactory> fixture) : AuthEndpointsTestsBase<LogoutTestFactory>(fixture)
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
    public async Task Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie()
    {
        var (_, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        await TestIsAuthorized(accessToken!);

        using var refreshResponse = await RefreshToken(refreshCookie);
        var (_, _, newAtToken) = await TestAndAddRtandAtAsync(refreshResponse);

        await TestIsAuthorized(newAtToken);
    }

    [Test, DependsOn(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie))]
    public async Task Logout_WithoutCookie_ShouldNotRevokeTokens()
    {
        var (refreshToken, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        await TestIsAuthorized(accessToken);

        using var logoutResponse = await LogoutAsync(null);
        await Assert.That(logoutResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var refreshResponse = await RefreshToken(refreshCookie);
        await CheckTokenFamilyRevokation(refreshToken, false, null);

        await TestIsAuthorized(accessToken);
    }

    [Test, DependsOn(nameof(Logout_WithoutCookie_ShouldNotRevokeTokens))]
    public async Task Logout_WithCookie_ShouldRevokeTokens()
    {
        var (refreshToken, refreshCookie, accessToken) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        await TestIsAuthorized(accessToken);

        using var logoutResponse = await LogoutAsync(refreshCookie);
        await Assert.That(logoutResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var refreshResponse = await RefreshToken(refreshCookie);
        await CheckTokenFamilyRevokation(refreshToken, true, "logout");

        await TestIsAuthorized(accessToken);
    }
}

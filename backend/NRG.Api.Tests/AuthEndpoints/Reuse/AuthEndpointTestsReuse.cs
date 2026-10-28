namespace NRG.Api.Tests.AuthEndpoints.Reuse;

public class ReuseTestFactory : AuthApiFactory
{
    override public string ContentRootPath => Path.Combine(RootPath, "Reuse");
}

[Category("Auth")]
[ClassDataSource<AuthApiTestFixture<ReuseTestFactory>>(Shared = SharedType.PerClass)]
public class AuthEndpointTestsReuse(AuthApiTestFixture<ReuseTestFactory> fixture) : AuthEndpointsTestsBase<ReuseTestFactory>(fixture)
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
    public async Task Refresh_WithValidRT_ShouldWorkAgain()
    {
        var (_, refreshCookie, _) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));

        using var refreshResponse = await RefreshToken(refreshCookie);
        var (_, _, newAtToken) = await TestAndAddRtandAtAsync(refreshResponse);

        await TestIsAuthorized(newAtToken);
    }

    [Test, DependsOn(nameof(Refresh_WithValidRT_ShouldWorkAgain))]
    public async Task Refresh_WithInvalidRT_ShouldRevokeAllRefreshTokens()
    {
        var (loginRT, loginRC, _) = await GetRtAndAtFromStateBag(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));
        using var loginResponse = await RefreshToken(loginRC);
        await TestRefreshReject(loginResponse, loginRT);

        var (refresh1RT, refresh1RC, _) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));
        using var refresh1Response = await RefreshToken(refresh1RC);
        await TestRefreshReject(refresh1Response, refresh1RT);

        var (refresh2RT, refresh2RC, _) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldWorkAgain));
        using var refresh2Response = await RefreshToken(refresh2RC);
        await TestRefreshReject(refresh2Response, refresh2RT);
    }

    [Test, DependsOn(nameof(Refresh_WithInvalidRT_ShouldRevokeAllRefreshTokens))]
    public async Task Access_AfterTokenRevocation_ShouldWork()
    {
        var (_, _, loginAT) = await GetRtAndAtFromStateBag(nameof(Login_WithCorrectCredentials_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));
        await TestIsAuthorized(loginAT);

        var (_, _, refresh1AT) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldGenerateAccessTokenAndSetRefreshTokenCookie));
        await TestIsAuthorized(refresh1AT);

        var (_, _, refresh2AT) = await GetRtAndAtFromStateBag(nameof(Refresh_WithValidRT_ShouldWorkAgain));
        await TestIsAuthorized(refresh2AT);
    }
}

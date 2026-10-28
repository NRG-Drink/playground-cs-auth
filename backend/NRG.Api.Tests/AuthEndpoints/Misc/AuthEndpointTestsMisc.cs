using System.Net;
using System.Security.Cryptography;

namespace NRG.Api.Tests.AuthEndpoints.Misc;

public class MiscTestFactory : AuthApiFactory
{
    override public string ContentRootPath => Path.Combine(RootPath, "Misc");
}

[Category("Auth")]
[ClassDataSource<AuthApiTestFixture<MiscTestFactory>>(Shared = SharedType.PerClass)]
public sealed class AuthEndpointTestsMisc(AuthApiTestFixture<MiscTestFactory> fixture) : AuthEndpointsTestsBase<MiscTestFactory>(fixture)
{
    [Test]
    public async Task Login_WithInvalidCredentials_ShouldFail()
    {
        await TestIsNotAuthorized();

        using var response = await LoginAsync("test", "wrongpassword");
        var content = await response.Content.ReadAsStringAsync();
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var headerValues);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(content).IsEmpty();
        await Assert.That(cookies).IsFalse();
        await Assert.That(headerValues).IsNull();

        await TestIsNotAuthorized();
    }

    [Test]
    public async Task Access_WithRandomToken_ShouldFail()
    {
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6InRlc3QtdXNlciIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6InRlc3QiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJVc2VyIiwiZXhwIjoxNzkzMDgwODE2LCJpc3MiOiJOUkcuQXBpIiwiYXVkIjoiTlJHLkFwaS5DbGllbnQifQ.QJRHZpmbQNaCIro8MB1jR8fZROSUxgUoGmkEJGlKZ44";
        await TestIsNotAuthorized(token);

        var randomToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await TestIsNotAuthorized(randomToken);
    }
}

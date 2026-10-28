using Microsoft.EntityFrameworkCore;
using NRG.Api.Security;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static NRG.Api.GraphQL.AuthTestQuery;
using static NRG.Api.GraphQL.NotesQuery;

namespace NRG.Api.Tests.AuthEndpoints;

public abstract class AuthEndpointsTestsBase<T>(ITestFixture<T> fixture) where T : AuthApiFactory, new()
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected async Task<HttpResponseMessage> LogoutAsync(string? refreshCookie)
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        if (refreshCookie is not null)
        {
            content.Headers.Add("Cookie", refreshCookie);
        }

        var logoutResponse = await fixture.Client.PostAsync("/auth/logout", content);
        return logoutResponse;
    }

    protected async Task<HttpResponseMessage> LoginAsync(string username, string password)
    {
        var loginRequest = new { Username = username, Password = password };
        var jsonContent = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");
        var response = await fixture.Client.PostAsync("/auth/login", jsonContent);

        return response;
    }

    protected async Task<HttpResponseMessage> RefreshToken(string refreshCookie)
    {
        var refreshContent = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        refreshContent.Headers.Add("Cookie", refreshCookie);
        var refreshResponse = await fixture.Client.PostAsync("/auth/refresh", refreshContent);

        return refreshResponse;
    }

    protected async Task<(string RtToken, string RtCookie, string AtToken)> GetRtAndAtFromStateBag(string contextName)
    {
        var contexts = TestContext.Current!.Dependencies.GetTests(contextName);
        var context = contexts.FirstOrDefault();
        await Assert.That(context).IsNotNull().Because($"Context {contextName} not found.");
        var rtToken = context.StateBag.Items[$"RefreshToken"] as string;
        var rtCookie = context.StateBag.Items[$"RefreshCookie"] as string;
        var atToken = context.StateBag.Items[$"AccessToken"] as string;
        if (rtToken is null || rtCookie is null || atToken is null)
        {
            throw new InvalidOperationException($"Refresh token, refresh cookie, or access token not found in state bag for context {contextName}.");
        }

        return (rtToken, rtCookie, atToken);
    }

    protected async Task TestRefreshReject(
        HttpResponseMessage response,
        string oldRefreshToken)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var setCookies = response.Headers.GetValues("Set-Cookie");
        var deleteCookie = await Assert.That(setCookies).HasSingleItem();
        var refreshToken = Uri.UnescapeDataString(deleteCookie.Split(';', 2)[0].Split('=', 2)[1]);
        await Assert.That(refreshToken).IsEmpty();

        await CheckTokenFamilyRevokation(oldRefreshToken, true, "reuse-detected");
    }

    protected async Task CheckTokenFamilyRevokation(
        string oldRefreshToken,
        bool isRevoked = true,
        string? lastRevokeReason = "logout")
    {
        // Token Family Revocation: Check that all refresh tokens in the database are revoked and have a reason for revocation
        using var db = fixture.Factory.DbFactory();
        var oldTokenHash = TokenService.HashTokenStatic(oldRefreshToken);
        var tokenFamilyId = db.RefreshTokens.FirstOrDefault(e => e.TokenHash == oldTokenHash)?.FamilyId;
        await Assert.That(tokenFamilyId).IsNotNull();

        var tokenFamily = await db.RefreshTokens.Where(e => e.FamilyId == tokenFamilyId).ToArrayAsync();
        if (isRevoked)
        {
            await Assert.That(tokenFamily)
                .ContainsOnly(e => e.IsActive == false)
                .And.ContainsOnly(e => e.Revoked is not null);
        }

        var rotatedTokens = tokenFamily.Where(e => e.ReasonRevoked == "rotated").ToArray();
        var lastRevokeTokens = tokenFamily.Where(e => e.ReasonRevoked == lastRevokeReason).ToArray();
        await Assert.That(rotatedTokens)
            .Count().IsEqualTo(tokenFamily.Length - 1)
            .And.ContainsOnly(e => e.IsActive == false)
            .And.ContainsOnly(e => e.Revoked is not null);
        await Assert.That(lastRevokeTokens)
            .Count().IsEqualTo(1)
            .And.ContainsOnly(e => e.IsActive == !isRevoked)
            .And.ContainsOnly(e => isRevoked ? e.Revoked is not null : e.Revoked is null);
    }

    protected async Task<(string RtToken, string RtCookie, string AtToken)> TestAndAddRtandAtAsync(
        HttpResponseMessage refreshResponse,
        bool isLogin = false)
    {
        // Check that the Set-Cookie header is present and contains the refresh token cookie with the correct options
        var (newRefreshToken, newRefreshCookie) = await GetAndTestRefreshCookie(refreshResponse, isLogin);
        TestContext.Current!.StateBag.Items[$"RefreshToken"] = newRefreshToken;
        TestContext.Current!.StateBag.Items[$"RefreshCookie"] = newRefreshCookie;

        //  Check that the response body contains a valid token and expiration date
        var content = await refreshResponse.Content.ReadAsStringAsync();
        var deserialised = await GetAndTestAccessToken(content);
        TestContext.Current!.StateBag.Items[$"AccessToken"] = deserialised.Token;

        return (newRefreshToken, newRefreshCookie, deserialised.Token);
    }

    protected async Task TestIsNotAuthorized(string? accessToken = null, string userName = "test-user")
    {
        await RequestHelloUnauthorized("User", accessToken);
    }

    protected async Task TestIsAuthorized(string? accessToken, string userName = "test-user")
    {
        await RequestHelloAuthorized("User", userName, accessToken);
        await RequestHelloAuthorized("Admin", userName, accessToken);
        await RequestHelloAuthorized("Employee", userName, accessToken);
        await RequestHelloUnauthorized("Staff", accessToken);
    }

    private async Task RequestHelloAuthorized(string name, string userName, string? accessToken)
    {
        var query = $"{{ hello{name} {{ hello timestamp }} }}";
        using var response = await PostGraphQl(fixture.Client, query, accessToken);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var dataElement = jsonDocument.RootElement.GetProperty("data").GetProperty($"hello{name}");
        var helloContent = dataElement.GetRawText();
        var graphqlResult = JsonSerializer.Deserialize<HelloData>(helloContent, JsonOptions);

        await Assert.That(graphqlResult)
            .IsNotNull()
            .And.Member(e => e.Hello, e => e.StartsWith($"Hello {name.ToLowerInvariant()}").And.EndsWith($"{userName}!"))
            .And.Member(e => e.Timestamp, e => e.IsBeforeOrEqualTo(DateTime.UtcNow));
    }

    private async Task RequestHelloUnauthorized(string name, string? accessToken)
    {
        var query = $"{{ hello{name} {{ hello timestamp }} }}";
        using var response = await PostGraphQl(fixture.Client, query, accessToken);

        var errors = await GetGraphQlErrors(response);
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors)
            .Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            .Or.Contains("not authorized", StringComparison.OrdinalIgnoreCase);
    }

    protected static async Task<TokenResponse> GetAndTestAccessToken(string content)
    {
        var deserialised = JsonSerializer.Deserialize<TokenResponse>(content, JsonOptions);
        await Assert.That(deserialised)
            .IsNotNull()
            .And.Member(e => e.Token, e => e.HasAtLeast(64))
            .And.Member(e => e.ExpiresAt, e => e.IsAfter(DateTimeOffset.UtcNow));

        return deserialised!;
    }

    protected string GetRefreshTokenFromCookie(string refreshCookie)
        => Uri.UnescapeDataString(refreshCookie.Split(';', 2)[0].Split('=', 2)[1]);

    protected async Task<(string RefreshToken, string RefreshCookie)> GetAndTestRefreshCookie(
        HttpResponseMessage response,
        bool isLogin)
    {
        var setCookies = response.Headers.GetValues("Set-Cookie");
        var refreshCookie = await Assert.That(setCookies).HasSingleItem();
        var refreshToken = GetRefreshTokenFromCookie(refreshCookie);
        await Assert.That(refreshCookie)
            .IsNotNull()
            .And.StartsWith($"{TokenService.RefreshTokenCookieName}=")
            .And.Contains($"Expires=", StringComparison.OrdinalIgnoreCase)
            .And.Contains($"Path={TokenService.CookiePath}", StringComparison.OrdinalIgnoreCase)
            .And.Contains("Secure", StringComparison.OrdinalIgnoreCase)
            .And.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase)
            .And.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase);

        using var db = fixture.Factory.DbFactory();
        var refreshTokenHash = TokenService.HashTokenStatic(refreshToken);
        var storedToken = await db.RefreshTokens
            .Include(e => e.ParentToken)
            .FirstOrDefaultAsync(e => e.TokenHash == refreshTokenHash);
        await Assert.That(storedToken)
            .IsNotNull()
            .And.Member(e => e.Created, e => e.IsBeforeOrEqualTo(DateTime.UtcNow))
            .And.Member(e => e.ExpiresAt, e => e.IsAfter(DateTime.UtcNow));

        if (!isLogin)
        {
            await Assert.That(storedToken.ParentToken)
                .IsNotNull()
                .And.Member(e => e.TokenHash, e => e.IsNotEqualTo(storedToken.TokenHash))
                .And.Member(e => e.Created, e => e.IsBefore(DateTime.UtcNow).And.IsBefore(storedToken.Created))
                .And.Member(e => e.ExpiresAt, e => e.IsBeforeOrEqualTo(storedToken.ExpiresAt))
                .And.Member(e => e.Revoked, e => e.IsNotNull().And.IsBeforeOrEqualTo(DateTime.UtcNow))
                .And.Member(e => e.ReasonRevoked, e => e.IsNotNull().And.IsEqualTo("rotated"));
        }

        return (refreshToken, refreshCookie);
    }

    protected static async Task<HttpResponseMessage> PostGraphQl(
        HttpClient client,
        string query,
        string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json")
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    protected static async Task<string> GetGraphQlErrors(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var errorsElement = jsonDocument.RootElement.GetProperty("errors");

        return errorsElement.GetRawText();
    }
}

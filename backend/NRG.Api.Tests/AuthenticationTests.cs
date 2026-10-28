using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NRG.Api.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TUnit.AspNetCore;

namespace NRG.Api.Tests;

public class BaseApiTest : WebApplicationTest<ApiFactory, Program>;

public class ApiFactory : TestWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<NotesDbContext>(options => options.UseSqlite(connection));
        });
    }
}

public sealed class AuthenticationTests : BaseApiTest
{
    [Test]
    public async Task HelloWorld_ReturnsHelloWorld()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/hello");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsEqualTo("Hello World!");
    }

    [Test]
    public async Task SecureHello_FailsNonAuthenticated()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/secure-hello");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SecureHello_ReturnsSecureHelloWorld_ClientDefaultHeader()
    {
        using var client = Factory.CreateClient();

        var usernamePassword = new { username = "alice", password = "alice-dev-password" };
        var upJson = JsonSerializer.Serialize(usernamePassword);
        var authContent = new StringContent(upJson, Encoding.UTF8, "application/json");

        using var tokenResponse = await client.PostAsync("/auth/token", authContent);
        var tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(tokenResponseString);
        var token = document.RootElement.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/secure-hello");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsEqualTo("Secure Hello World!");
    }

    [Test]
    public async Task SecureHello_ReturnsSecureHelloWorld_RequestHeader()
    {
        using var client = Factory.CreateClient();

        var usernamePassword = new { username = "alice", password = "alice-dev-password" };
        var upJson = JsonSerializer.Serialize(usernamePassword);
        var authContent = new StringContent(upJson, Encoding.UTF8, "application/json");

        using var tokenResponse = await client.PostAsync("/auth/token", authContent);
        var tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(tokenResponseString);
        var token = document.RootElement.GetProperty("accessToken").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure-hello");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsEqualTo("Secure Hello World!");
    }

    [Test]
    public async Task GraphQl_LoginIssuesToken_AndRejectsInvalidCredentials()
    {
        using var client = Factory.CreateClient();

        using var login = await PostGraphQl(client, "mutation { login(username: \"alice\", password: \"alice-dev-password\") { accessToken expiresAt } }");
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(login)).IsFalse();
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDocument.RootElement.GetProperty("data").GetProperty("login").GetProperty("accessToken").GetString()!;

        using var authorized = await PostGraphQl(client, "{ workbooks { id } }", token);
        await Assert.That(await HasGraphQlErrors(authorized)).IsFalse();

        using var invalid = await PostGraphQl(client, "mutation { login(username: \"alice\", password: \"wrong\") { accessToken } }");
        await Assert.That(invalid.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(invalid)).IsTrue();
    }

    [Test]
    public async Task GraphQl_RejectsAnonymousAndInvalidBearerTokens()
    {
        using var client = Factory.CreateClient();

        using var anonymous = await PostGraphQl(client, "{ workbooks { id } }");
        await Assert.That(anonymous.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(HasGraphQlErrors(anonymous)).IsTrue();

        using var invalid = await PostGraphQl(client, "{ workbooks { id } }", "not-a-jwt");
        await Assert.That(invalid.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(HasGraphQlErrors(invalid)).IsTrue();
    }

    [Test]
    public async Task GraphQl_AllowsValidToken_ButOnlyAdminCanQueryWorkbookCount()
    {
        using var client = Factory.CreateClient();
        var userToken = await GetToken(client, "alice", "alice-dev-password");
        var adminToken = await GetToken(client, "admin", "admin");

        using var userDenied = await PostGraphQl(client, "{ workbookCount }", userToken);
        using var adminAllowed2 = await PostGraphQl(client, "{ workbooks { id } }", adminToken);
        using var adminAllowed = await PostGraphQl(client, "{ workbookCount }", adminToken);
        using var userAllowed = await PostGraphQl(client, "{ workbooks { id } }", userToken);

        await Assert.That(userDenied.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(userDenied)).IsTrue();
        await Assert.That(adminAllowed2.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(adminAllowed2)).IsFalse();
        await Assert.That(adminAllowed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(adminAllowed)).IsFalse();
        await Assert.That(userAllowed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await HasGraphQlErrors(userAllowed)).IsFalse();
    }

    private static async Task<HttpResponseMessage> PostGraphQl(
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

    private static async Task<string> GetToken(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsync("/auth/token",
            new StringContent(JsonSerializer.Serialize(new { username, password }), Encoding.UTF8,
                "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<bool> HasGraphQlErrors(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0;
    }
}

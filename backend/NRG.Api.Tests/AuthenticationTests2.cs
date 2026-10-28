using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NRG.Api.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TUnit.AspNetCore;
using TUnit.Core.Interfaces;

namespace NRG.Api.Tests;

public sealed class ApiTestFixture : IAsyncInitializer, IAsyncDisposable
{
    public ApiFactory2 Factory { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return ValueTask.CompletedTask;
    }
}

public class ApiFactory2 : TestWebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NotesDbContext>();
            services.RemoveAll<DbContextOptions<NotesDbContext>>();
            services.AddDbContext<NotesDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

[ClassDataSource<ApiTestFixture>(Shared = SharedType.PerClass)]
public class AuthenticationTests2(ApiTestFixture fixture)
{
    [Test]
    public async Task GraphQl_HidesOtherUsersData_AddWorkbooks()
    {
        var aliceToken = await GetToken(fixture.Client, "alice", "alice-dev-password");
        var bobToken = await GetToken(fixture.Client, "bob", "bob-dev-password");
        TestContext.Current!.StateBag.Items["AliceToken"] = aliceToken;
        TestContext.Current!.StateBag.Items["BobToken"] = bobToken;

        var createdWorkbookAlice = await PostGraphQl(
            fixture.Client,
            "mutation { createWorkbook(name: \"Alice private\") { id } }",
            aliceToken);
        var workbookIdAlice = await GetInt(createdWorkbookAlice, "createWorkbook", "id");
        TestContext.Current!.StateBag.Items["AliceWorkbookId"] = workbookIdAlice;

        var createdWorkbookBob = await PostGraphQl(
            fixture.Client,
            "mutation { createWorkbook(name: \"Alice will never know\") { id } }",
            bobToken);
        var workbookIdBob = await GetInt(createdWorkbookBob, "createWorkbook", "id");
        TestContext.Current!.StateBag.Items["BobWorkbookId"] = workbookIdBob;
    }

    [Test, DependsOn(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))]
    public async Task GraphQl_HidesOtherUsersData_AddNotes()
    {
        var workbookContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))[0];
        var aliceToken = workbookContext.StateBag.Items["AliceToken"] as string;
        var bobToken = workbookContext.StateBag.Items["BobToken"] as string;
        var workbookIdAlice = workbookContext.StateBag.Items["AliceWorkbookId"] as int?;
        var workbookIdBob = workbookContext.StateBag.Items["BobWorkbookId"] as int?;

        var createNoteAlice = await PostGraphQl(fixture.Client, $"mutation {{ createNote(workbookId: {workbookIdAlice}, title: \"Alice's Note\", content: \"This is a note for Alice's workbook.\") {{ id }} }}", aliceToken);
        var noteIdAlice = await GetInt(createNoteAlice, "createNote", "id");
        TestContext.Current!.StateBag.Items["AliceNoteId"] = noteIdAlice;

        var createNoteBob = await PostGraphQl(fixture.Client, $"mutation {{ createNote(workbookId: {workbookIdBob}, title: \"Bob's Note\", content: \"This is a note for Bob's workbook.\") {{ id }} }}", bobToken);
        var noteIdBob = await GetInt(createNoteBob, "createNote", "id");
        TestContext.Current!.StateBag.Items["BobNoteId"] = noteIdBob;
    }

    [Test, DependsOn(nameof(GraphQl_HidesOtherUsersData_AddNotes))]
    public async Task GraphQl_HidelUserData_AddComments()
    {
        var workbookContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))[0];
        var notesContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddNotes))[0];
        var aliceToken = workbookContext.StateBag.Items["AliceToken"] as string;
        var bobToken = workbookContext.StateBag.Items["BobToken"] as string;
        var noteIdAlice = notesContext.StateBag.Items["AliceNoteId"] as int?;
        var noteIdBob = notesContext.StateBag.Items["BobNoteId"] as int?;

        var createCommentAlice = await PostGraphQl(fixture.Client, $"mutation {{ createComment(noteId: {noteIdAlice}, content: \"This is a comment for Alice's note.\") {{ id }} }}", aliceToken);
        var commentIdAlice = await GetInt(createCommentAlice, "createComment", "id");
        TestContext.Current!.StateBag.Items["AliceCommentId"] = commentIdAlice;

        var createCommentBob = await PostGraphQl(fixture.Client, $"mutation {{ createComment(noteId: {noteIdBob}, content: \"This is a comment for Bob's note.\") {{ id }} }}", bobToken);
        var commentIdBob = await GetInt(createCommentBob, "createComment", "id");
        TestContext.Current!.StateBag.Items["BobCommentId"] = commentIdBob;
    }

    [Test, DependsOn(nameof(GraphQl_HidelUserData_AddComments))]
    public async Task GraphQl_HidelUserData_CheckWorkbookTree()
    {
        var workbookContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))[0];
        var notesContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddNotes))[0];
        var commentsContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidelUserData_AddComments))[0];
        var aliceToken = workbookContext.StateBag.Items["AliceToken"] as string;
        var bobToken = workbookContext.StateBag.Items["BobToken"] as string;
        var workbookIdAlice = workbookContext.StateBag.Items["AliceWorkbookId"] as int?;
        var workbookIdBob = workbookContext.StateBag.Items["BobWorkbookId"] as int?;
        var noteIdAlice = notesContext.StateBag.Items["AliceNoteId"] as int?;
        var noteIdBob = notesContext.StateBag.Items["BobNoteId"] as int?;
        var commentIdAlice = commentsContext.StateBag.Items["AliceCommentId"] as int?;
        var commentIdBob = commentsContext.StateBag.Items["BobCommentId"] as int?;

        // Alice
        var objectsAlice = await PostGraphQl(fixture.Client, "{ workbooks { id name notes { id title content comments { id content } } } }", aliceToken);

        var workbooksAlice = await GetJsonElement(objectsAlice, "workbooks");
        await Assert.That(workbooksAlice.GetArrayLength()).IsEqualTo(1);
        var workbookIdAlice2 = workbooksAlice[0].GetProperty("id").GetInt32();
        await Assert.That(workbookIdAlice2).IsEqualTo(workbookIdAlice);

        var notesAlice = workbooksAlice[0].GetProperty("notes");
        await Assert.That(notesAlice.GetArrayLength()).IsEqualTo(1);
        var noteIdAlice2 = notesAlice[0].GetProperty("id").GetInt32();
        await Assert.That(noteIdAlice2).IsEqualTo(noteIdAlice);

        var commentsAlice = notesAlice[0].GetProperty("comments");
        await Assert.That(commentsAlice.GetArrayLength()).IsEqualTo(1);
        var commentIdAlice2 = commentsAlice[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdAlice2).IsEqualTo(commentIdAlice);

        // Bob
        var objectsBob = await PostGraphQl(fixture.Client, "{ workbooks { id name notes { id title content comments { id content } } } }", bobToken);
        
        var workbooksBob = await GetJsonElement(objectsBob, "workbooks");
        await Assert.That(workbooksBob.GetArrayLength()).IsEqualTo(1);
        var workbookIdBob2 = workbooksBob[0].GetProperty("id").GetInt32();
        await Assert.That(workbookIdBob2).IsEqualTo(workbookIdBob);

        var notesBob = workbooksBob[0].GetProperty("notes");
        await Assert.That(notesBob.GetArrayLength()).IsEqualTo(1);
        var noteIdBob2 = notesBob[0].GetProperty("id").GetInt32();
        await Assert.That(noteIdBob2).IsEqualTo(noteIdBob);

        var commentsBob = notesBob[0].GetProperty("comments");
        await Assert.That(commentsBob.GetArrayLength()).IsEqualTo(1);
        var commentIdBob2 = commentsBob[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdBob2).IsEqualTo(commentIdBob);
    }

    [Test, DependsOn(nameof(GraphQl_HidelUserData_AddComments))]
    public async Task GraphQl_HidelUserData_CheckNoteTree()
    {
        var workbookContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))[0];
        var notesContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddNotes))[0];
        var commentsContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidelUserData_AddComments))[0];
        var aliceToken = workbookContext.StateBag.Items["AliceToken"] as string;
        var bobToken = workbookContext.StateBag.Items["BobToken"] as string;
        var noteIdAlice = notesContext.StateBag.Items["AliceNoteId"] as int?;
        var noteIdBob = notesContext.StateBag.Items["BobNoteId"] as int?;
        var commentIdAlice = commentsContext.StateBag.Items["AliceCommentId"] as int?;
        var commentIdBob = commentsContext.StateBag.Items["BobCommentId"] as int?;

        // Alice
        var objectsAlice = await PostGraphQl(fixture.Client, "{ notes { id title content comments { id content } } }", aliceToken);

        var notesAlice = await GetJsonElement(objectsAlice, "notes");
        await Assert.That(notesAlice.GetArrayLength()).IsEqualTo(1);
        var noteIdAlice2 = notesAlice[0].GetProperty("id").GetInt32();
        await Assert.That(noteIdAlice2).IsEqualTo(noteIdAlice);

        var commentsAlice = notesAlice[0].GetProperty("comments");
        await Assert.That(commentsAlice.GetArrayLength()).IsEqualTo(1);
        var commentIdAlice2 = commentsAlice[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdAlice2).IsEqualTo(commentIdAlice);

        // Bob
        var objectsBob = await PostGraphQl(fixture.Client, "{ notes { id title content comments { id content } } }", bobToken);

        var notesBob = await GetJsonElement(objectsBob, "notes");
        await Assert.That(notesBob.GetArrayLength()).IsEqualTo(1);
        var noeteIdBob2 = notesBob[0].GetProperty("id").GetInt32();
        await Assert.That(noeteIdBob2).IsEqualTo(noteIdBob);

        var commentsBob = notesBob[0].GetProperty("comments");
        await Assert.That(commentsBob.GetArrayLength()).IsEqualTo(1);
        var commentIdBob2 = commentsBob[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdBob2).IsEqualTo(commentIdBob);
    }

    [Test, DependsOn(nameof(GraphQl_HidelUserData_AddComments))]
    public async Task GraphQl_HidelUserData_CheckCommentTree()
    {
        var workbookContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddWorkbooks))[0];
        var notesContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidesOtherUsersData_AddNotes))[0];
        var commentsContext = TestContext.Current!.Dependencies.GetTests(nameof(GraphQl_HidelUserData_AddComments))[0];
        var aliceToken = workbookContext.StateBag.Items["AliceToken"] as string;
        var bobToken = workbookContext.StateBag.Items["BobToken"] as string;
        var commentIdAlice = commentsContext.StateBag.Items["AliceCommentId"] as int?;
        var commentIdBob = commentsContext.StateBag.Items["BobCommentId"] as int?;

        // Alice
        var objectsAlice = await PostGraphQl(fixture.Client, "{ comments { id content } }", aliceToken);

        var commentsAlice = await GetJsonElement(objectsAlice, "comments");
        await Assert.That(commentsAlice.GetArrayLength()).IsEqualTo(1);
        var commentIdAlice2 = commentsAlice[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdAlice2).IsEqualTo(commentIdAlice);

        // Bob
        var objectsBob = await PostGraphQl(fixture.Client, "{ comments { id content } }", bobToken);

        var commentsBob = await GetJsonElement(objectsBob, "comments");
        await Assert.That(commentsBob.GetArrayLength()).IsEqualTo(1);
        var commentIdBob2 = commentsBob[0].GetProperty("id").GetInt32();
        await Assert.That(commentIdBob2).IsEqualTo(commentIdBob);
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

    private static async Task<int> GetInt(HttpResponseMessage response, string field, string property)
    {
        using (response)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("data").GetProperty(field).GetProperty(property).GetInt32();
        }
    }

    private static async Task<JsonElement> GetJsonElement(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty(field).Clone();
    }
}

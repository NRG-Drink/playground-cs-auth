using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using NRG.Api.Data;
using NRG.Api.Security;
using System.Security.Claims;

namespace NRG.Api.GraphQL;

[MutationType]
public partial class NotesMutation
{
    [AllowAnonymous]
    public DemoTokenResponse Login(
        string username,
        string password,
        [Service] DemoTokenService tokens)
    {
        return tokens.Issue(new DemoLoginRequest(username, password))
            ?? throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid username or password.")
                .SetCode("UNAUTHENTICATED")
                .Build());
    }

    [Authorize]
    public async Task<Workbook> CreateWorkbook(
        string name,
        [Service] NotesDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var workbook = new Workbook { Name = name, OwnerId = GetUserId(claimsPrincipal) };
        await db.Workbooks.AddAsync(workbook, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return workbook;
    }

    [Authorize]
    public async Task<Note> CreateNote(
        int workbookId,
        string title,
        string content,
        [Service] NotesDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var workbook = await db.Workbooks.FirstAsync(e => e.Id == workbookId, cancellationToken);
        var note = new Note { WorkbookId = workbook.Id, Workbook = workbook, Title = title, Content = content };
        await db.Notes.AddAsync(note, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return note;
    }

    [Authorize]
    public async Task<Comment> CreateComment(
        int noteId,
        string content,
        [Service] NotesDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var note = await db.Notes.FirstAsync(e => e.Id == noteId, cancellationToken);
        var comment = new Comment
        {
            NoteId = note.Id,
            Note = note,
            AuthorId = GetUserId(claimsPrincipal),
            Content = content
        };
        await db.Comments.AddAsync(comment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return comment;
    }

    private static string GetUserId(ClaimsPrincipal claimsPrincipal)
        => claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? throw new GraphQLException("The token has no subject claim.");
}

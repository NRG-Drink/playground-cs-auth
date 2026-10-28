using GreenDonut.Data;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using NRG.Api.Data;

namespace NRG.Api.GraphQL;

[QueryType]
public partial class NotesQuery
{
    [Authorize]
    public async Task<IQueryable<Workbook>> GetWorkbooks(
        [Service] NotesDbContext db,
        QueryContext<Workbook> query,
        //ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        return db.Workbooks
            .With(query.Include(e => e.Id))
            .AsNoTracking();
    }

    [Authorize]
    public async Task<IQueryable<Note>> GetNotes(
        [Service] NotesDbContext db,
        QueryContext<Note> query,
        CancellationToken cancellationToken)
    {
        return db.Notes
            .With(query.Include(e => e.Id))
            .AsNoTracking();
    }

    [Authorize]
    public async Task<IQueryable<Comment>> GetComments(
        [Service] NotesDbContext db,
        QueryContext<Comment> query,
        CancellationToken cancellationToken)
    {
        return db.Comments
            .With(query.Include(e => e.Id))
            .AsNoTracking();
    }

    [Authorize(Roles = ["Admin"])]
    public async Task<int> GetWorkbookCount(
        [Service] NotesDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.Workbooks.CountAsync(cancellationToken);
    }
}

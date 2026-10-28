using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace NRG.Api.Data;

public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public NotesDbContext(DbContextOptions<NotesDbContext> options, IHttpContextAccessor httpContextAccessor) : this(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CurrentUserId => _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Workbook> Workbooks => Set<Workbook>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(token => token.Id);

            e.Property(e => e.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(e => e.CreatedByIp).IsRequired().HasMaxLength(45);
            e.Property(e => e.RevokedByIp).HasMaxLength(45);
            e.Property(e => e.ReasonRevoked).HasMaxLength(100);

            e.HasOne(e => e.ParentToken)
              .WithMany()
              .HasForeignKey(e => e.ParentTokenId)
              .OnDelete(DeleteBehavior.ClientSetNull);

            // Lookup on every refresh — must be unique and indexed
            e.HasIndex(e => e.TokenHash).IsUnique();
            // Family-wide revocation query
            e.HasIndex(e => e.FamilyId);
            // Cleanup job
            e.HasIndex(e => e.ExpiresAt);
            e.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(user => user.Id);
            e.HasIndex(user => user.UserId).IsUnique();
            e.HasIndex(user => user.Username).IsUnique();
        });

        modelBuilder.Entity<Workbook>(e =>
        {
            e.HasKey(workbook => workbook.Id);
            e.HasIndex(workbook => workbook.OwnerId);
            e.HasQueryFilter(workbook => workbook.OwnerId == CurrentUserId);
            e.HasMany(workbook => workbook.Notes)
                .WithOne(note => note.Workbook)
                .HasForeignKey(note => note.WorkbookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(note => note.Id);
            e.HasQueryFilter(note => note.Workbook.OwnerId == CurrentUserId);
            e.HasMany(note => note.Comments)
                .WithOne(comment => comment.Note)
                .HasForeignKey(comment => comment.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.HasKey(comment => comment.Id);
            e.HasQueryFilter(comment => comment.Note.Workbook.OwnerId == CurrentUserId);
        });
    }
}

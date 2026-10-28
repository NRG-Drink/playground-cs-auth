using System.ComponentModel.DataAnnotations.Schema;

namespace NRG.Api.Data;

public class RefreshToken
{
    public int Id { get; set; }

    // SHA-256 hash of the raw token — never store the raw value
    public string TokenHash { get; set; } = null!;

    // Rotation chain from one login (for reuse detection)
    public Guid FamilyId { get; set; }

    public string UserId { get; set; } = null!;

    public DateTime Created { get; set; }
    public string CreatedByIp { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }

    public DateTime? Revoked { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReasonRevoked { get; set; } // "rotated" | "reuse-detected" | "logout"

    public int? ParentTokenId { get; set; }
    public RefreshToken? ParentToken { get; set; }

    [NotMapped] public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    [NotMapped] public bool IsActive => Revoked is null && !IsExpired;
}

public sealed class User
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
}

public sealed class Workbook
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Name { get; set; }
    public ICollection<Note> Notes { get; set; } = [];
}

public sealed class Note
{
    public int Id { get; set; }
    public int WorkbookId { get; set; }
    public required Workbook Workbook { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public ICollection<Comment> Comments { get; set; } = [];
}

public sealed class Comment
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public required Note Note { get; set; }
    public required string AuthorId { get; set; }
    public required string Content { get; set; }
}

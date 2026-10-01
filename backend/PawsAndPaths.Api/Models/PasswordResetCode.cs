using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class PasswordResetCode
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    [Required, MaxLength(64)] public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UsedAt { get; set; }
    public int FailedAttempts { get; set; }
}

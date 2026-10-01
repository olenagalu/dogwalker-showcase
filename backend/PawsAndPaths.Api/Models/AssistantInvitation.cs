using System.ComponentModel.DataAnnotations;
namespace PawsAndPaths.Api.Models;
public class AssistantInvitation
{
    public int Id { get; set; }
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(254)] public required string Email { get; set; }
    [MaxLength(64)] public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

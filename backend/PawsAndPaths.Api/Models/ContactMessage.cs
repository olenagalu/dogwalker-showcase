using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class ContactMessage
{
    public int Id { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(254)] public required string Email { get; set; }
    [MaxLength(3000)] public required string Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class SiteContent
{
    [Key, MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];
    [MaxLength(3000)]
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

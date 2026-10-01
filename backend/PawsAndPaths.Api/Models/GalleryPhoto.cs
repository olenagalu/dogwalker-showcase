using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class GalleryPhoto
{
    public int Id { get; set; }
    [MaxLength(160)] public string Caption { get; set; } = string.Empty;
    [MaxLength(100)] public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

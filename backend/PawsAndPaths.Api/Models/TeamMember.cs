using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class TeamMember
{
    public int Id { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(120)] public string Role { get; set; } = string.Empty;
    [MaxLength(1500)] public string Bio { get; set; } = string.Empty;
    [MaxLength(100)] public string PhotoContentType { get; set; } = string.Empty;
    public byte[] PhotoData { get; set; } = [];
    public int DisplayOrder { get; set; }
}

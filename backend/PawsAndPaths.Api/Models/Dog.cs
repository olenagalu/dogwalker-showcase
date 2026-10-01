using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class Dog
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    [MaxLength(80)] public required string Name { get; set; }
    [MaxLength(80)] public string Breed { get; set; } = string.Empty;
    [Range(0, 30)] public int? Age { get; set; }
    [MaxLength(2000)] public string CareInstructions { get; set; } = string.Empty;
    [MaxLength(1500)] public string BehavioralNotes { get; set; } = string.Empty;
    [MaxLength(1500)] public string MedicalNotes { get; set; } = string.Empty;
    [MaxLength(100)] public string PhotoContentType { get; set; } = string.Empty;
    public byte[] PhotoData { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}

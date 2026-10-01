using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawsAndPaths.Api.Models;

public class ServiceOffering
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(1000)] public required string Description { get; set; }
    [Range(5, 1440)] public int DurationMinutes { get; set; }
    [Column(TypeName = "numeric(10,2)")] public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOvernightStay { get; set; }
    public ICollection<Booking> Bookings { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawsAndPaths.Api.Models;

public class Booking
{
    public int Id { get; set; }
    public string? AssistantId { get; set; }
    public AppUser? Assistant { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int DogId { get; set; }
    public Dog Dog { get; set; } = null!;
    public int ServiceOfferingId { get; set; }
    public ServiceOffering ServiceOffering { get; set; } = null!;
    public DateOnly Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernightStay { get; set; }
    public TimeOnly? OvernightStartTime { get; set; }
    public TimeOnly? OvernightEndTime { get; set; }
    public TimeOnly? MiddayStartTime { get; set; }
    public TimeOnly? MiddayEndTime { get; set; }
    [Column(TypeName = "numeric(10,2)")] public decimal Price { get; set; }
    [MaxLength(2000)] public string SpecialInstructions { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Completed,
    Cancelled,
    Declined
}

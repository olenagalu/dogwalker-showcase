using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.Models;

public class AvailabilityRule
{
    public int Id { get; set; }
    public string? AssistantId { get; set; }
    public AppUser? Assistant { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public DateOnly? SpecificDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
    [MaxLength(300)] public string Notes { get; set; } = string.Empty;
}

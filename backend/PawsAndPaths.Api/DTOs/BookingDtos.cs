using System.ComponentModel.DataAnnotations;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.DTOs;

public record CreateBookingDto(
    [Range(1, int.MaxValue)] int DogId,
    [Range(1, int.MaxValue)] int ServiceId,
    DateOnly Date,
    TimeOnly StartTime,
    [MaxLength(2000)] string? SpecialInstructions,
    DateOnly? EndDate = null,
    TimeOnly? OvernightStartTime = null,
    TimeOnly? OvernightEndTime = null,
    TimeOnly? MiddayStartTime = null,
    TimeOnly? MiddayEndTime = null,
    string? AssistantId = null);

public record CreateOwnerBookingDto(
    [Required] string CustomerId,
    [Range(1, int.MaxValue)] int DogId,
    [Range(1, int.MaxValue)] int ServiceId,
    DateOnly Date,
    TimeOnly StartTime,
    [MaxLength(2000)] string? SpecialInstructions,
    DateOnly? EndDate = null,
    TimeOnly? OvernightStartTime = null,
    TimeOnly? OvernightEndTime = null,
    TimeOnly? MiddayStartTime = null,
    TimeOnly? MiddayEndTime = null,
    string? AssistantId = null);

public record UpdateOvernightScheduleDto(
    TimeOnly OvernightStartTime,
    TimeOnly OvernightEndTime,
    TimeOnly MiddayStartTime,
    TimeOnly MiddayEndTime);

public record UpdateBookingStatusDto(
    BookingStatus Status,
    DeclineEmailOption DeclineEmailOption = DeclineEmailOption.Automatic,
    [MaxLength(160)] string? CustomEmailSubject = null,
    [MaxLength(5000)] string? CustomEmailMessage = null);

public enum DeclineEmailOption
{
    Automatic,
    Custom
}

public record BookingDto(
    int Id, string CustomerName, string CustomerEmail, string CustomerPhone, string CustomerServiceAddress,
    int DogId, string DogName, string DogBreed,
    int ServiceId, string ServiceName, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    decimal Price, string SpecialInstructions, BookingStatus Status, DateTimeOffset CreatedAt,
    DateOnly? EndDate, bool IsOvernightStay, TimeOnly? OvernightStartTime,
    TimeOnly? OvernightEndTime, TimeOnly? MiddayStartTime, TimeOnly? MiddayEndTime,
    string CustomerId, AccountApprovalStatus CustomerApprovalStatus, string? AssistantId = null, string? AssistantName = null);

public static class BookingPricing
{
    public static int Nights(DateOnly start, DateOnly? end) => end is null ? 1 : Math.Max(1, end.Value.DayNumber - start.DayNumber);
}

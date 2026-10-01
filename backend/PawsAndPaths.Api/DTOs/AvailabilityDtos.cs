using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record AvailabilityWriteDto(
    DayOfWeek? DayOfWeek,
    DateOnly? SpecificDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAvailable,
    [MaxLength(300)] string? Notes);

public record AvailabilityDto(
    int Id, DayOfWeek? DayOfWeek, DateOnly? SpecificDate,
    TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable, string Notes, DateOnly? EndDate = null);

public record AvailableSlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

public record PublicScheduleSegmentDto(TimeOnly StartTime, string Status, bool IsBookable);

public record OvernightAvailabilityDto(
    DateOnly CheckIn, DateOnly Checkout, bool IsAvailable,
    IReadOnlyList<DateOnly> UnavailableDates);

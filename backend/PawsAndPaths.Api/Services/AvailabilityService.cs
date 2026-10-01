using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface IAvailabilityService
{
    Task<bool> IsAvailableAsync(DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId,
        CancellationToken cancellationToken, bool enforceRegularHours = true, string? assistantId = null);
    Task<IReadOnlyList<AvailableSlotDto>> GetSlotsAsync(DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken, string? assistantId = null);
    Task<IReadOnlyList<PublicScheduleSegmentDto>> GetDayScheduleAsync(DateOnly date, int serviceId, CancellationToken cancellationToken, string? assistantId = null);
    Task<OvernightAvailabilityDto?> CheckOvernightAsync(
        DateOnly checkIn, DateOnly checkout, int serviceId, CancellationToken cancellationToken, string? assistantId = null);
}

public class AvailabilityService(AppDbContext db) : IAvailabilityService
{

    public async Task<bool> IsAvailableAsync(
        DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId,
        CancellationToken cancellationToken, bool enforceRegularHours = true, string? assistantId = null)
    {
        if (!await IsActiveSitterAsync(assistantId, cancellationToken)) return false;
        if (end <= start || date < DateOnly.FromDateTime(DateTime.Today)) return false;
        var hours = await WorkingHours.ReadAsync(db, cancellationToken);
        if (enforceRegularHours && (start < hours.Start || end > hours.End)) return false;

        // Within regular hours, owner-created rules remove additional time.
        var blocked = await RulesForDate(date, assistantId).Where(rule => !rule.IsAvailable).ToListAsync(cancellationToken);
        if (blocked.Any(rule => Overlaps(start, end, rule.StartTime, rule.EndTime))) return false;

        if (assistantId != null && !(await RulesForDate(date, assistantId).Where(r => r.IsAvailable).ToListAsync(cancellationToken))
            .Any(r => r.StartTime <= start && r.EndTime >= end)) return false;

        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.AssistantId == assistantId && booking.Id != excludeBookingId
                && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date == date)
                    || (booking.IsOvernightStay && booking.Date <= date && booking.EndDate >= date)))
            .ToListAsync(cancellationToken);
        return !bookings.SelectMany(BookingSchedule.Windows)
            .Any(window => window.Date == date && Overlaps(start, end, window.StartTime, window.EndTime));
    }

    public async Task<IReadOnlyList<AvailableSlotDto>> GetSlotsAsync(
        DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken, string? assistantId = null)
    {
        if (!await IsActiveSitterAsync(assistantId, cancellationToken)) return [];
        if (to < from || to.DayNumber - from.DayNumber > 60) return [];
        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken);
        if (service is null) return [];
        if (service.IsOvernightStay) return [];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var hours = await WorkingHours.ReadAsync(db, cancellationToken);
        var blockedRules = await db.Availability.AsNoTracking()
            .Where(rule => rule.AssistantId == assistantId && !rule.IsAvailable).ToListAsync(cancellationToken);
        var weeklyRules = await db.Availability.AsNoTracking().Where(r => r.AssistantId == assistantId && r.IsAvailable).ToListAsync(cancellationToken);
        var activeBookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.AssistantId == assistantId && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date >= from && booking.Date <= to)
                    || (booking.IsOvernightStay && booking.Date <= to && booking.EndDate >= from)))
            .ToListAsync(cancellationToken);
        var bookingWindows = activeBookings.SelectMany(BookingSchedule.Windows)
            .Where(window => window.Date >= from && window.Date <= to)
            .GroupBy(window => window.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var slots = new List<AvailableSlotDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date < today) continue;
            var blocked = blockedRules.Where(rule =>
                (rule.SpecificDate <= date && (rule.EndDate ?? rule.SpecificDate) >= date) || rule.DayOfWeek == date.DayOfWeek).ToList();
            var windows = bookingWindows.GetValueOrDefault(date, []);
            // Regular appointments begin no earlier than 6 AM and must finish
            // by 11 PM. Overnight stays use their separate care schedule.
            for (var startMinutes = hours.Start.Hour * 60 + hours.Start.Minute;
                 startMinutes + service.DurationMinutes <= hours.End.Hour * 60 + hours.End.Minute;
                 startMinutes += 30)
            {
                var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
                var endMinutes = startMinutes + service.DurationMinutes;
                var end = new TimeOnly(endMinutes / 60, endMinutes % 60);
                if ((assistantId == null || weeklyRules.Any(r => r.DayOfWeek == date.DayOfWeek && r.StartTime <= start && r.EndTime >= end))
                    && !blocked.Any(rule => Overlaps(start, end, rule.StartTime, rule.EndTime))
                    && !windows.Any(window => Overlaps(start, end, window.StartTime, window.EndTime)))
                    slots.Add(new AvailableSlotDto(date, start, end));
            }
        }
        return slots.Distinct().OrderBy(slot => slot.Date).ThenBy(slot => slot.StartTime).ToList();
    }

    public async Task<IReadOnlyList<PublicScheduleSegmentDto>> GetDayScheduleAsync(
        DateOnly date, int serviceId, CancellationToken cancellationToken, string? assistantId = null)
    {
        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken);
        if (service is null) return [];

        if (assistantId != null)
        {
            var slots = await GetSlotsAsync(date, date, serviceId, cancellationToken, assistantId);
            var starts = slots.Select(slot => slot.StartTime).ToHashSet();
            return Enumerable.Range(0, 48).Select(index =>
            {
                var start = new TimeOnly(index / 2, index % 2 * 30);
                var available = starts.Contains(start);
                return new PublicScheduleSegmentDto(start, available ? "Available" : "Unavailable", available);
            }).ToList();
        }

        var blocked = await RulesForDate(date, assistantId).Where(rule => !rule.IsAvailable)
            .ToListAsync(cancellationToken);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.AssistantId == assistantId && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed)
                && ((!booking.IsOvernightStay && booking.Date == date)
                    || (booking.IsOvernightStay && booking.Date <= date && booking.EndDate >= date)))
            .ToListAsync(cancellationToken);
        var bookingWindows = bookings.SelectMany(BookingSchedule.Windows)
            .Where(window => window.Date == date).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var segments = new List<PublicScheduleSegmentDto>();
        var hours = await WorkingHours.ReadAsync(db, cancellationToken);

        for (var startMinutes = 0; startMinutes < 24 * 60; startMinutes += 30)
        {
            var start = new TimeOnly(startMinutes / 60, startMinutes % 60);
            var segmentEnd = startMinutes == 23 * 60 + 30
                ? TimeOnly.MaxValue
                : start.AddMinutes(30);
            var booked = bookingWindows.Any(window =>
                Overlaps(start, segmentEnd, window.StartTime, window.EndTime));
            var unavailable = blocked.Any(rule =>
                Overlaps(start, segmentEnd, rule.StartTime, rule.EndTime));
            var withinRegularHours = service.IsOvernightStay
                || (start >= hours.Start && segmentEnd <= hours.End);
            var emergency = !service.IsOvernightStay && date >= today && hours.IsEmergency(start);
            var status = booked ? "Booked" : unavailable ? "Unavailable"
                : emergency ? "Emergency" : withinRegularHours ? "Available" : "Unavailable";

            var appointmentEndMinutes = startMinutes + service.DurationMinutes;
            var appointmentFitsHours = start >= hours.Start && appointmentEndMinutes <= hours.End.Hour * 60 + hours.End.Minute;
            var bookable = !service.IsOvernightStay && date >= today
                && status == "Available" && appointmentFitsHours;
            if (bookable)
            {
                var appointmentEnd = new TimeOnly(appointmentEndMinutes / 60, appointmentEndMinutes % 60);
                bookable = !bookingWindows.Any(window =>
                        Overlaps(start, appointmentEnd, window.StartTime, window.EndTime))
                    && !blocked.Any(rule =>
                        Overlaps(start, appointmentEnd, rule.StartTime, rule.EndTime));
            }

            segments.Add(new PublicScheduleSegmentDto(start, status, bookable));
        }

        return segments;
    }

    public async Task<OvernightAvailabilityDto?> CheckOvernightAsync(
        DateOnly checkIn, DateOnly checkout, int serviceId, CancellationToken cancellationToken, string? assistantId = null)
    {
        var service = await db.Services.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == serviceId && item.IsActive && item.IsOvernightStay, cancellationToken);
        if (service is null) return null;

        var preview = new Booking
        {
            UserId = "availability-check",
            ServiceOfferingId = serviceId,
            Date = checkIn,
            EndDate = checkout,
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(9, 0),
            IsOvernightStay = true,
            OvernightStartTime = new TimeOnly(22, 0),
            OvernightEndTime = new TimeOnly(9, 0),
            MiddayStartTime = new TimeOnly(14, 0),
            MiddayEndTime = new TimeOnly(15, 0)
        };
        var unavailableDates = new HashSet<DateOnly>();
        foreach (var window in BookingSchedule.Windows(preview))
            if (!await IsAvailableAsync(window.Date, window.StartTime, window.EndTime,
                    null, cancellationToken, false, assistantId))
                unavailableDates.Add(window.Date);

        return new OvernightAvailabilityDto(
            checkIn, checkout, unavailableDates.Count == 0,
            unavailableDates.OrderBy(date => date).ToList());
    }

    private IQueryable<AvailabilityRule> RulesForDate(DateOnly date, string? assistantId) =>
        db.Availability.Where(rule => rule.AssistantId == assistantId && ((rule.SpecificDate <= date && (rule.EndDate ?? rule.SpecificDate) >= date) || rule.DayOfWeek == date.DayOfWeek));

    private Task<bool> IsActiveSitterAsync(string? assistantId, CancellationToken ct) => assistantId == null
        ? Task.FromResult(true)
        : db.Users.AnyAsync(u => u.Id == assistantId && u.AssistantStatus == AssistantAccountStatus.Active, ct);

    private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && endA > startB;
}

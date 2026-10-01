using System.Data;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface IBookingService
{
    Task<(Booking? Booking, string? Error)> CreateAsync(
        string userId, CreateBookingDto request, CancellationToken cancellationToken,
        BookingStatus initialStatus = BookingStatus.Pending);
    Task<(Booking? Booking, string? Error)> ChangeStatusAsync(int bookingId, BookingStatus status, CancellationToken cancellationToken);
    Task<(Booking? Booking, string? Error)> UpdateOvernightScheduleAsync(
        int bookingId, UpdateOvernightScheduleDto request, CancellationToken cancellationToken);
}

public class BookingService(AppDbContext db, IAvailabilityService availability) : IBookingService
{
    public async Task<(Booking? Booking, string? Error)> CreateAsync(
        string userId, CreateBookingDto request, CancellationToken cancellationToken,
        BookingStatus initialStatus = BookingStatus.Pending)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var dog = await db.Dogs.SingleOrDefaultAsync(item => item.Id == request.DogId && item.UserId == userId, cancellationToken);
        if (dog is null) return (null, "The selected dog does not belong to this account.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return (null, "Customer account not found.");
        if (user.ApprovalStatus == AccountApprovalStatus.Declined)
            return (null, "Booking is not available for this service address.");
        if (string.IsNullOrWhiteSpace(user.ServiceArea) || string.IsNullOrWhiteSpace(user.ServiceAddress))
            return (null, "Add your service area and address to your profile before booking.");

        var service = await db.Services.SingleOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken);
        if (service is null) return (null, "The selected service is unavailable.");
        if (request.Date < DateOnly.FromDateTime(DateTime.Today)) return (null, "Booking dates cannot be in the past.");

        var overnightStart = request.OvernightStartTime ?? new TimeOnly(22, 0);
        var overnightEnd = request.OvernightEndTime ?? new TimeOnly(9, 0);
        var middayStart = request.MiddayStartTime ?? new TimeOnly(14, 0);
        var middayEnd = request.MiddayEndTime ?? new TimeOnly(15, 0);
        var end = request.StartTime.AddMinutes(service.DurationMinutes);
        if (service.IsOvernightStay)
        {
            if (request.EndDate is null || request.EndDate <= request.Date || request.EndDate.Value.DayNumber - request.Date.DayNumber > 60)
                return (null, "Choose an overnight checkout date within 60 days after check-in.");
            if (middayEnd <= middayStart || overnightStart <= overnightEnd)
                return (null, "Overnight care must cross midnight and the midday end must be after its start.");
            var preview = new Booking
            {
                UserId = userId, DogId = dog.Id, ServiceOfferingId = service.Id,
                Date = request.Date, EndDate = request.EndDate, StartTime = overnightStart,
                EndTime = overnightEnd, IsOvernightStay = true,
                OvernightStartTime = overnightStart, OvernightEndTime = overnightEnd,
                MiddayStartTime = middayStart, MiddayEndTime = middayEnd
            };
            foreach (var window in BookingSchedule.Windows(preview))
                if (!await availability.IsAvailableAsync(window.Date, window.StartTime, window.EndTime, null, cancellationToken, false, request.AssistantId))
                    return (null, $"Overnight care is unavailable on {window.Date:MMM d}.");
            end = overnightEnd;
        }
        else if (!await availability.IsAvailableAsync(request.Date, request.StartTime, end, null, cancellationToken, true, request.AssistantId))
            return (null, "That time is no longer available.");

        var booking = new Booking
        {
            UserId = userId,
            AssistantId = request.AssistantId,
            DogId = dog.Id,
            ServiceOfferingId = service.Id,
            Date = request.Date,
            EndDate = service.IsOvernightStay ? request.EndDate : null,
            StartTime = service.IsOvernightStay ? overnightStart : request.StartTime,
            EndTime = end,
            IsOvernightStay = service.IsOvernightStay,
            OvernightStartTime = service.IsOvernightStay ? overnightStart : null,
            OvernightEndTime = service.IsOvernightStay ? overnightEnd : null,
            MiddayStartTime = service.IsOvernightStay ? middayStart : null,
            MiddayEndTime = service.IsOvernightStay ? middayEnd : null,
            Price = service.IsOvernightStay
                ? service.Price * BookingPricing.Nights(request.Date, request.EndDate)
                : service.Price,
            SpecialInstructions = request.SpecialInstructions?.Trim() ?? string.Empty,
            Status = initialStatus
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (booking, null);
    }

    public async Task<(Booking? Booking, string? Error)> UpdateOvernightScheduleAsync(
        int bookingId, UpdateOvernightScheduleDto request, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (booking is null || !booking.IsOvernightStay || booking.EndDate is null)
            return (null, "Overnight booking not found.");
        if (request.MiddayEndTime <= request.MiddayStartTime || request.OvernightStartTime <= request.OvernightEndTime)
            return (null, "Overnight care must cross midnight and the midday end must be after its start.");
        var previous = (booking.OvernightStartTime, booking.OvernightEndTime, booking.MiddayStartTime, booking.MiddayEndTime);
        booking.OvernightStartTime = request.OvernightStartTime;
        booking.OvernightEndTime = request.OvernightEndTime;
        booking.MiddayStartTime = request.MiddayStartTime;
        booking.MiddayEndTime = request.MiddayEndTime;
        foreach (var window in BookingSchedule.Windows(booking))
            if (!await availability.IsAvailableAsync(window.Date, window.StartTime, window.EndTime, booking.Id, cancellationToken, false, booking.AssistantId))
            {
                (booking.OvernightStartTime, booking.OvernightEndTime, booking.MiddayStartTime, booking.MiddayEndTime) = previous;
                return (null, $"The new overnight schedule conflicts on {window.Date:MMM d}.");
            }
        booking.StartTime = request.OvernightStartTime;
        booking.EndTime = request.OvernightEndTime;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (booking, null);
    }

    public async Task<(Booking? Booking, string? Error)> ChangeStatusAsync(
        int bookingId, BookingStatus status, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var booking = await db.Bookings
            .Include(item => item.Assistant).Include(item => item.User).Include(item => item.Dog).Include(item => item.ServiceOffering)
            .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        if (booking is null) return (null, "Booking not found.");

        if (status == BookingStatus.Confirmed)
        {
            if (booking.User.ApprovalStatus != AccountApprovalStatus.Approved)
                return (null, "Approve this customer's service area before confirming the booking.");
            foreach (var window in BookingSchedule.Windows(booking))
                if (!await availability.IsAvailableAsync(window.Date, window.StartTime, window.EndTime, booking.Id, cancellationToken, !booking.IsOvernightStay, booking.AssistantId))
                    return (null, "This booking now conflicts with availability or another active booking.");
        }

        booking.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (booking, null);
    }
}

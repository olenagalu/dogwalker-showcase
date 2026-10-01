using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Authorize(Roles = AppRoles.Customer + "," + AppRoles.Owner)]
[Route("api/bookings")]
public class BookingsController(
    AppDbContext db,
    IBookingService bookingService,
    IBookingDecisionEmailSender bookingDecisionEmailSender,
    IOwnerNotificationEmailSender notificationSender,
    ILogger<BookingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = await Query().Where(booking => booking.UserId == userId)
            .OrderByDescending(booking => booking.Date).ThenBy(booking => booking.StartTime)
            .ToListAsync(cancellationToken);
        return Ok(bookings.Select(booking => booking.ToDto()));
    }

    [HttpGet("{id:int}/assistant-photo")]
    public async Task<IActionResult> AssistantPhoto(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Assistant)
            .SingleOrDefaultAsync(b => b.Id == id && (b.UserId == userId || User.IsInRole(AppRoles.Owner)), cancellationToken);
        if (booking?.Assistant is not { ProfilePhotoData.Length: > 0 } assistant) return NotFound();
        // Existing customers retain the sitter identity on historical bookings, even when frozen.
        Response.Headers.CacheControl = "private, no-store";
        return File(assistant.ProfilePhotoData, assistant.ProfilePhotoContentType);
    }

    [HttpGet("admin"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetAll(CancellationToken cancellationToken)
    {
        var bookings = await Query().OrderBy(booking => booking.Date).ThenBy(booking => booking.StartTime)
            .ToListAsync(cancellationToken);
        return Ok(bookings.Select(booking => booking.ToDto()));
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (booking, error) = await bookingService.CreateAsync(userId, request, cancellationToken);
        if (booking is null) return Conflict(new { message = error });
        var complete = await Query().SingleAsync(item => item.Id == booking.Id, cancellationToken);
        try
        {
            await notificationSender.SendBookingRequestAsync(new BookingNotification(
                complete.User.FullName,
                complete.User.Email ?? string.Empty,
                complete.User.PhoneNumber ?? string.Empty,
                complete.User.ServiceAddress,
                complete.Dog.Name,
                complete.Dog.Breed,
                complete.ServiceOffering.Name,
                complete.Date,
                complete.EndDate,
                complete.StartTime,
                complete.EndTime,
                complete.Price,
                complete.SpecialInstructions), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Owner email notification failed for booking {BookingId}.", complete.Id);
        }
        return CreatedAtAction(nameof(GetMine), new { id = booking.Id }, complete.ToDto());
    }

    [HttpPost("admin"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<BookingDto>> CreateForCustomer(
        CreateOwnerBookingDto request, CancellationToken cancellationToken)
    {
        var bookingRequest = new CreateBookingDto(
            request.DogId, request.ServiceId, request.Date,
            request.StartTime, request.SpecialInstructions, request.EndDate,
            request.OvernightStartTime, request.OvernightEndTime,
            request.MiddayStartTime, request.MiddayEndTime, request.AssistantId);
        var (booking, error) = await bookingService.CreateAsync(
            request.CustomerId, bookingRequest, cancellationToken, BookingStatus.Confirmed);
        if (booking is null) return Conflict(new { message = error });
        var complete = await Query().SingleAsync(item => item.Id == booking.Id, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = booking.Id }, complete.ToDto());
    }

    [HttpPut("{id:int}/overnight-schedule"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<BookingDto>> UpdateOvernightSchedule(
        int id, UpdateOvernightScheduleDto request, CancellationToken cancellationToken)
    {
        var (booking, error) = await bookingService.UpdateOvernightScheduleAsync(id, request, cancellationToken);
        if (booking is null) return Conflict(new { message = error });
        var complete = await Query().SingleAsync(item => item.Id == booking.Id, cancellationToken);
        return Ok(complete.ToDto());
    }

    [HttpPut("{id:int}/status"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<BookingDto>> ChangeStatus(
        int id, UpdateBookingStatusDto request, CancellationToken cancellationToken)
    {
        var allowed = new[] { BookingStatus.Confirmed, BookingStatus.Declined, BookingStatus.Cancelled, BookingStatus.Completed };
        if (!allowed.Contains(request.Status)) return BadRequest(new { message = "Invalid owner status change." });
        if (request.Status == BookingStatus.Declined
            && request.DeclineEmailOption == DeclineEmailOption.Custom
            && (string.IsNullOrWhiteSpace(request.CustomEmailSubject)
                || string.IsNullOrWhiteSpace(request.CustomEmailMessage)))
            return BadRequest(new { message = "Add both a subject and message for the custom decline email." });
        var previousStatus = await db.Bookings.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => (BookingStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        var (booking, error) = await bookingService.ChangeStatusAsync(id, request.Status, cancellationToken);
        if (booking is null) return Conflict(new { message = error });
        if (request.Status == BookingStatus.Declined && previousStatus != BookingStatus.Declined
            && !string.IsNullOrWhiteSpace(booking.User.Email))
        {
            try
            {
                await bookingDecisionEmailSender.SendDeclinedAsync(
                    new BookingDeclinedNotification(
                        booking.User.Email,
                        booking.User.FullName,
                        booking.Dog.Name,
                        booking.ServiceOffering.Name,
                        booking.Date),
                    request.DeclineEmailOption == DeclineEmailOption.Custom
                        ? request.CustomEmailSubject?.Trim()
                        : null,
                    request.DeclineEmailOption == DeclineEmailOption.Custom
                        ? request.CustomEmailMessage?.Trim()
                        : null,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Declined-booking email could not be sent for booking {BookingId}.", booking.Id);
            }
        }
        return Ok(booking.ToDto());
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelMine(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (booking is null) return NotFound();
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            return Conflict(new { message = "This booking can no longer be cancelled." });
        if (booking.Date < DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
            return Conflict(new { message = "Please contact Princess Dog Walker for same-day cancellations." });
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<Booking> Query() => db.Bookings.AsNoTracking()
        .Include(booking => booking.Assistant).Include(booking => booking.User).Include(booking => booking.Dog).Include(booking => booking.ServiceOffering);
}

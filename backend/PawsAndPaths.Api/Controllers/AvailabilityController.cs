using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/availability")]
public class AvailabilityController(AppDbContext db, IAvailabilityService availabilityService) : ControllerBase
{
    [HttpGet("hours")]
    public async Task<IActionResult> Hours(CancellationToken ct) => Ok(await WorkingHours.ReadAsync(db, ct));

    [HttpPut("hours"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> SaveHours(WorkingHours hours, CancellationToken ct)
    {
        if (hours.End <= hours.Start || hours.EmergencyStart == hours.EmergencyEnd)
            return BadRequest(new { message = "Regular closing time must follow opening time; emergency start and end must differ." });
        var row = await db.SiteContent.FindAsync([WorkingHours.Key], ct);
        if (row is null) { row = new SiteContent { Key = WorkingHours.Key }; db.SiteContent.Add(row); }
        row.Text = System.Text.Json.JsonSerializer.Serialize(hours);
        row.ContentType = "application/json";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(hours);
    }

    public record BlockRange(DateOnly From, DateOnly To, TimeOnly Start, TimeOnly End, string? Notes);

    [HttpPost("blocks"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> BlockDates(BlockRange request, CancellationToken ct)
    {
        if (request.To < request.From || request.To.DayNumber - request.From.DayNumber > 366 || request.End <= request.Start || request.Notes?.Length > 300)
            return BadRequest(new { message = "Choose a date range up to one year and an end time after the start time." });
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
            db.Availability.Add(new AvailabilityRule { SpecificDate = date, StartTime = request.Start,
                EndTime = request.End, IsAvailable = false, Notes = request.Notes?.Trim() ?? "" });
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Time blocked. Existing bookings remain on the schedule." });
    }
    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<AvailableSlotDto>>> Slots(
        DateOnly from, DateOnly to, int serviceId, CancellationToken cancellationToken, string? assistantId = null) =>
        Ok(await availabilityService.GetSlotsAsync(from, to, serviceId, cancellationToken, assistantId));

    [HttpGet("day")]
    public async Task<ActionResult<IReadOnlyList<PublicScheduleSegmentDto>>> Day(
        DateOnly date, int serviceId, CancellationToken cancellationToken, string? assistantId = null) =>
        Ok(await availabilityService.GetDayScheduleAsync(date, serviceId, cancellationToken, assistantId));

    [HttpGet("overnight")]
    public async Task<ActionResult<OvernightAvailabilityDto>> Overnight(
        DateOnly checkIn, DateOnly checkout, int serviceId, CancellationToken cancellationToken, string? assistantId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (checkIn < today || checkout <= checkIn || checkout.DayNumber - checkIn.DayNumber > 60)
            return BadRequest(new { message = "Choose a checkout date within 60 days after check-in." });
        var result = await availabilityService.CheckOvernightAsync(
            checkIn, checkout, serviceId, cancellationToken, assistantId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityDto>>> GetRules(CancellationToken cancellationToken) =>
        Ok((await db.Availability.AsNoTracking().Where(rule => rule.AssistantId == null && !rule.IsAvailable)
            .OrderBy(rule => rule.SpecificDate).ThenBy(rule => rule.DayOfWeek)
            .ThenBy(rule => rule.StartTime).ToListAsync(cancellationToken)).Select(rule => rule.ToDto()));

    [HttpPost, Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<AvailabilityDto>> Create(AvailabilityWriteDto request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var rule = new AvailabilityRule();
        Apply(rule, request);
        db.Availability.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule.ToDto());
    }

    [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<AvailabilityDto>> Update(int id, AvailabilityWriteDto request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var rule = await db.Availability.FindAsync([id], cancellationToken);
        if (rule is null || rule.AssistantId != null) return NotFound();
        Apply(rule, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rule.ToDto());
    }

    [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var rule = await db.Availability.FindAsync([id], cancellationToken);
        if (rule is null || rule.AssistantId != null) return NotFound();
        db.Availability.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool Validate(AvailabilityWriteDto request)
    {
        if ((request.DayOfWeek is null) == (request.SpecificDate is null))
            ModelState.AddModelError("scope", "Choose either a recurring weekday or one specific date.");
        if (request.EndTime <= request.StartTime)
            ModelState.AddModelError(nameof(request.EndTime), "End time must be after start time.");
        return ModelState.IsValid;
    }

    private static void Apply(AvailabilityRule rule, AvailabilityWriteDto request)
    {
        rule.DayOfWeek = request.DayOfWeek;
        rule.SpecificDate = request.SpecificDate;
        rule.StartTime = request.StartTime;
        rule.EndTime = request.EndTime;
        // Persisted rules are owner-created exceptions that remove additional
        // time from the normal 6 AM–11 PM schedule and overnight care windows.
        rule.IsAvailable = false;
        rule.Notes = request.Notes?.Trim() ?? string.Empty;
    }
}

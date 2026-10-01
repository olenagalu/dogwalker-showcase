using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Route("api/assistants")]
public class AssistantsController(AppDbContext db) : ControllerBase
{
    public record StatusRequest(AssistantAccountStatus Status);
    public record WeeklyDay(DayOfWeek DayOfWeek, bool IsAvailable, TimeOnly StartTime, TimeOnly EndTime);
    public record BlockRequest(DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, DateOnly? EndDate = null);

    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Users.AsNoTracking()
        .Where(u => u.AssistantStatus != null).OrderBy(u => u.FullName)
        .Select(u => new { u.Id, u.FullName, u.Email, Status = u.AssistantStatus, HasProfilePhoto = u.ProfilePhotoData.Length > 0 }).ToListAsync(ct));

    [HttpGet("public")]
    public async Task<IActionResult> Public(CancellationToken ct) => Ok(await db.Users.AsNoTracking()
        .Where(u => u.AssistantStatus == AssistantAccountStatus.Active).OrderBy(u => u.FullName)
        .Select(u => new { u.Id, u.FullName, HasProfilePhoto = u.ProfilePhotoData.Length > 0 }).ToListAsync(ct));

    [HttpGet("{id}/photo")]
    public async Task<IActionResult> Photo(string id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.AssistantStatus == AssistantAccountStatus.Active, ct);
        if (user is null || user.ProfilePhotoData.Length == 0) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(user.ProfilePhotoData, user.ProfilePhotoContentType);
    }

    [HttpPut("{id}/status"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Status(string id, StatusRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.AssistantStatus != null, ct);
        if (user is null) return NotFound();
        user.AssistantStatus = request.Status;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("me/bookings"), Authorize(Roles = AppRoles.Assistant)]
    public async Task<IActionResult> Bookings(CancellationToken ct)
    {
        var target = await Scope("me", ct);
        if (target is null) return Forbid();
        var bookings = await db.Bookings.AsNoTracking().Where(b => b.AssistantId == target)
            .Include(b => b.User).Include(b => b.Dog).Include(b => b.ServiceOffering).Include(b => b.Assistant)
            .OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToListAsync(ct);
        return Ok(bookings.Select(b => b.ToDto()));
    }

    [HttpGet("me"), Authorize(Roles = AppRoles.Assistant)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (user.AssistantStatus != AssistantAccountStatus.Active) return Forbid();
        return Ok(new { user.Id, user.FullName, user.Email, Status = user.AssistantStatus, HasProfilePhoto = user.ProfilePhotoData.Length > 0 });
    }

    [HttpPut("me/photo"), Authorize(Roles = AppRoles.Assistant), RequestSizeLimit(ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? photo, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (user.AssistantStatus != AssistantAccountStatus.Active) return Forbid();
        var upload = await ImageUpload.ReadAsync(photo, ct);
        if (upload.Error is not null) return BadRequest(new { message = upload.Error });
        user.ProfilePhotoData = upload.Data!; user.ProfilePhotoContentType = upload.ContentType!;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("me/photo"), Authorize(Roles = AppRoles.Assistant)]
    public async Task<IActionResult> RemovePhoto(CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (user.AssistantStatus != AssistantAccountStatus.Active) return Forbid();
        user.ProfilePhotoData = []; user.ProfilePhotoContentType = "";
        await db.SaveChangesAsync(ct); return NoContent();
    }

    // "me" derives scope from the authenticated subject; other IDs require the owner role.
    private async Task<string?> Scope(string id, CancellationToken ct)
    {
        var target = id == "me" ? User.FindFirstValue(ClaimTypes.NameIdentifier) : id;
        if (!User.IsInRole(AppRoles.Owner) && (id != "me" || !User.IsInRole(AppRoles.Assistant))) return null;
        return await db.Users.AnyAsync(u => u.Id == target && u.AssistantStatus != null
            && (u.AssistantStatus == AssistantAccountStatus.Active || User.IsInRole(AppRoles.Owner)), ct) ? target : null;
    }

    [HttpGet("{id}/availability"), Authorize(Roles = AppRoles.Assistant + "," + AppRoles.Owner)]
    public async Task<IActionResult> Availability(string id, CancellationToken ct)
    {
        var target = await Scope(id, ct); if (target is null) return Forbid();
        return Ok((await db.Availability.AsNoTracking().Where(r => r.AssistantId == target)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.SpecificDate).ToListAsync(ct)).Select(r => r.ToDto()));
    }

    [HttpPut("{id}/availability/weekly"), Authorize(Roles = AppRoles.Assistant + "," + AppRoles.Owner)]
    public async Task<IActionResult> Weekly(string id, List<WeeklyDay> days, CancellationToken ct)
    {
        var target = await Scope(id, ct); if (target is null) return Forbid();
        if (days.Select(d => d.DayOfWeek).Distinct().Count() != 7
            || days.Any(d => !Enum.IsDefined(d.DayOfWeek) || (d.IsAvailable && d.EndTime <= d.StartTime)))
            return BadRequest(new { message = "Supply all seven days with valid available intervals." });
        foreach (var group in days.GroupBy(d => d.DayOfWeek))
        {
            var intervals = group.OrderBy(d => d.StartTime).ToList();
            if ((intervals.Count > 1 && intervals.Any(d => !d.IsAvailable))
                || intervals.Zip(intervals.Skip(1)).Any(pair => pair.First.EndTime > pair.Second.StartTime))
                return BadRequest(new { message = "Intervals on the same day must not overlap or mix available and unavailable entries." });
        }
        db.Availability.RemoveRange(await db.Availability.Where(r => r.AssistantId == target && r.DayOfWeek != null).ToListAsync(ct));
        foreach (var day in days.Where(d => d.IsAvailable))
            db.Availability.Add(new AvailabilityRule { AssistantId = target, DayOfWeek = day.DayOfWeek,
                StartTime = day.StartTime, EndTime = day.EndTime, IsAvailable = true });
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("{id}/availability/blocks"), Authorize(Roles = AppRoles.Assistant + "," + AppRoles.Owner)]
    public Task<IActionResult> AddBlock(string id, BlockRequest request, CancellationToken ct) => SaveBlock(id, null, request, ct);

    [HttpPut("{id}/availability/blocks/{blockId:int}"), Authorize(Roles = AppRoles.Assistant + "," + AppRoles.Owner)]
    public Task<IActionResult> EditBlock(string id, int blockId, BlockRequest request, CancellationToken ct) => SaveBlock(id, blockId, request, ct);

    private async Task<IActionResult> SaveBlock(string id, int? blockId, BlockRequest request, CancellationToken ct)
    {
        var target = await Scope(id, ct); if (target is null) return Forbid();
        var start = request.StartTime ?? TimeOnly.MinValue; var end = request.EndTime ?? TimeOnly.MaxValue;
        if (request.EndDate < request.Date)
            return BadRequest(new { message = "The end date must be on or after the start date." });
        if ((request.StartTime is null) != (request.EndTime is null) || end <= start)
            return BadRequest(new { message = "Choose an end after the start, or leave both times blank to block the whole day." });
        var rule = blockId is null ? new AvailabilityRule { AssistantId = target } : await db.Availability.SingleOrDefaultAsync(r => r.Id == blockId && r.AssistantId == target && r.SpecificDate != null, ct);
        if (rule is null) return NotFound();
        rule.SpecificDate = request.Date; rule.EndDate = request.EndDate; rule.StartTime = start; rule.EndTime = end; rule.IsAvailable = false;
        if (blockId is null) db.Availability.Add(rule);
        await db.SaveChangesAsync(ct); return Ok(rule.ToDto());
    }

    [HttpDelete("{id}/availability/blocks/{blockId:int}"), Authorize(Roles = AppRoles.Assistant + "," + AppRoles.Owner)]
    public async Task<IActionResult> DeleteBlock(string id, int blockId, CancellationToken ct)
    {
        var target = await Scope(id, ct); if (target is null) return Forbid();
        var rule = await db.Availability.SingleOrDefaultAsync(r => r.Id == blockId && r.AssistantId == target && r.SpecificDate != null, ct);
        if (rule is null) return NotFound();
        db.Availability.Remove(rule); await db.SaveChangesAsync(ct); return NoContent();
    }
}

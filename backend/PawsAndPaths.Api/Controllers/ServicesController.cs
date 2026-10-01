using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceDto>>> GetAll(
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var canSeeInactive = includeInactive && User.IsInRole(AppRoles.Owner);
        var query = db.Services.AsNoTracking().AsQueryable();
        if (!canSeeInactive) query = query.Where(service => service.IsActive);
        return Ok((await query
            .OrderBy(service => service.Price)
            .ThenBy(service => service.DurationMinutes)
            .ThenBy(service => service.Name)
            .ToListAsync(cancellationToken))
            .Select(service => service.ToDto()));
    }

    [HttpPost, Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<ServiceDto>> Create(ServiceWriteDto request, CancellationToken cancellationToken)
    {
        var service = new ServiceOffering { Name = request.Name.Trim(), Description = request.Description.Trim() };
        Apply(service, request);
        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        return Created("/api/services", service.ToDto());
    }

    [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<ServiceDto>> Update(int id, ServiceWriteDto request, CancellationToken cancellationToken)
    {
        var service = await db.Services.FindAsync([id], cancellationToken);
        if (service is null) return NotFound();
        Apply(service, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(service.ToDto());
    }

    [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var service = await db.Services.FindAsync([id], cancellationToken);
        if (service is null) return NotFound();
        if (await db.Bookings.AnyAsync(booking => booking.ServiceOfferingId == id, cancellationToken))
            return Conflict(new { message = "Services with booking history can be disabled but not deleted." });
        db.Services.Remove(service);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(ServiceOffering service, ServiceWriteDto request)
    {
        service.Name = request.Name.Trim();
        service.Description = request.Description.Trim();
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.IsActive = request.IsActive;
        service.IsOvernightStay = request.IsOvernightStay;
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Route("api/emergency")]
public class EmergencyController(AppDbContext db, IOwnerNotificationEmailSender sender,
    ILogger<EmergencyController> logger) : ControllerBase
{
    public record EmergencyRequest([Required, MaxLength(120)] string Name,
        [Required, EmailAddress, MaxLength(254)] string Email,
        [Required, MaxLength(30)] string Phone, [Required, MaxLength(300)] string Address,
        [Required, MaxLength(80)] string Dog, [MaxLength(80)] string? Breed,
        int ServiceId, DateOnly Date, TimeOnly Time, [MaxLength(1000)] string? Notes);

    [HttpPost, EnableRateLimiting("contact")]
    public async Task<IActionResult> Create(EmergencyRequest request, CancellationToken ct)
    {
        var hours = await WorkingHours.ReadAsync(db, ct);
        if (!hours.IsEmergency(request.Time) || request.Date < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { message = "Choose a future date and a time within the current emergency request hours." });
        var service = await db.Services.SingleOrDefaultAsync(x => x.Id == request.ServiceId && x.IsActive && !x.IsOvernightStay, ct);
        if (service is null) return BadRequest(new { message = "Choose an active daytime service." });
        var message = new ContactMessage { Name = request.Name.Trim(), Email = request.Email.Trim(),
            Message = $"Emergency care request — awaiting Demo Owner’s approval\nDate: {request.Date}\nTime: {request.Time}\nService: {service.Name}\nService price: ${service.Price:0.00}\nExtra charge: ${hours.EmergencySurcharge:0.00}\nTotal if approved: ${service.Price + hours.EmergencySurcharge:0.00}\nPhone: {request.Phone}\nAddress: {request.Address}\nDog: {request.Dog}\nBreed: {request.Breed}\nNotes: {request.Notes}" };
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(ct);
        try { await sender.SendContactMessageAsync(new(message.Name, message.Email, message.Message), ct); }
        catch (Exception error) { logger.LogError(error, "Emergency request email failed for message {Id}", message.Id); }
        return Ok(new { message = "Request saved for Demo Owner’s review." });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(
    AppDbContext db,
    IOwnerNotificationEmailSender notificationSender,
    ILogger<ContactController> logger) : ControllerBase
{
    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await db.ContactMessages.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.Name, item.Email, item.Message, item.CreatedAt })
            .ToListAsync(cancellationToken));

    [HttpPost, EnableRateLimiting("contact")]
    public async Task<IActionResult> Create(ContactMessageDto request, CancellationToken cancellationToken)
    {
        var message = new ContactMessage
        {
            Name = request.Name.Trim(), Email = request.Email.Trim().ToLowerInvariant(), Message = request.Message.Trim()
        };
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await notificationSender.SendContactMessageAsync(
                new ContactNotification(message.Name, message.Email, message.Message), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Owner email notification failed for contact message {MessageId}.", message.Id);
        }
        return Ok(new { message = "Thanks for reaching out. Demo Owner will be in touch soon." });
    }
}

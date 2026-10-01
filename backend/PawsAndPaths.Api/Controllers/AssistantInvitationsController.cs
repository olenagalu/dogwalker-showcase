using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Route("api/assistant-invitations")]
public class AssistantInvitationsController(AppDbContext db, UserManager<AppUser> users,
    IAssistantInvitationEmailSender emailSender, ILogger<AssistantInvitationsController> logger) : ControllerBase
{
    public record InviteRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, MaxLength(120)] string FullName);
    public record AcceptRequest([Required, MaxLength(128)] string Token,
        [Required, EmailAddress, MaxLength(254)] string Email,
        [Required, MaxLength(120)] string FullName,
        [Required, MinLength(8), MaxLength(100)] string Password);

    [HttpGet, Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await db.AssistantInvitations.AsNoTracking()
        .Where(i => i.UsedAt == null && i.ExpiresAt > DateTimeOffset.UtcNow)
        .OrderByDescending(i => i.CreatedAt).Select(i => new { i.Id, i.FullName, i.Email, i.CreatedAt, i.ExpiresAt }).ToListAsync(ct));

    [HttpPost, Authorize(Roles = AppRoles.Owner), EnableRateLimiting("account")]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return BadRequest(new { message = "Enter the assistant’s name (up to 120 characters)." });
        if (await users.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "An account with this email already exists." });
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new AssistantInvitation { FullName = name, Email = email, TokenHash = Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(48) };
        db.AssistantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        try { await emailSender.SendAsync(email, token, ct); }
        catch (Exception ex)
        {
            db.AssistantInvitations.Remove(invitation);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(ex, "Assistant invitation email delivery failed.");
            return StatusCode(503, new { message = "Invitation could not be sent. Check SMTP and PublicBaseUrl, then try again." });
        }
        return Ok(new { message = "Invitation sent. It expires in 48 hours." });
    }

    [HttpPost("accept"), EnableRateLimiting("account")]
    public async Task<IActionResult> Accept(AcceptRequest request, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var hash = Hash(request.Token);
        var email = request.Email.Trim().ToLowerInvariant();
        var invitation = await db.AssistantInvitations.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null || invitation.UsedAt != null || invitation.ExpiresAt <= DateTimeOffset.UtcNow
            || invitation.Email != email || await users.FindByEmailAsync(email) is not null)
            return BadRequest(new { message = "This invitation is invalid, expired, already used, or belongs to a different email." });
        var user = new AppUser { Email = invitation.Email, UserName = invitation.Email,
            FullName = request.FullName.Trim(), EmailConfirmed = true, AssistantStatus = AssistantAccountStatus.Active };
        if (user.FullName.Length == 0) return BadRequest(new { message = "Enter your name." });
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        result = await users.AddToRoleAsync(user, AppRoles.Assistant);
        if (!result.Succeeded) throw new InvalidOperationException("Assistant role assignment failed.");
        // Consume all invitations to this email in the same transaction as account creation.
        foreach (var item in await db.AssistantInvitations.Where(i => i.Email == email && i.UsedAt == null).ToListAsync(ct))
            item.UsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(new { message = "Assistant account created. Sign in with your email and password." });
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

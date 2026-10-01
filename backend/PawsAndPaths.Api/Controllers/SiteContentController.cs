using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController]
[Route("api/site-content")]
public class SiteContentController(AppDbContext db) : ControllerBase
{
    private const string AboutPhotoKey = "about-photo";
    private const string OwnerDescriptionKey = "owner-description";
    private const int MaximumPhotoBytes = 5 * 1024 * 1024;
    private static readonly OwnerProfileContentRequest DefaultOwnerProfile = new(
        "A familiar face for your best friend",
        "Personal care starts with trust.",
        "Hi, I’m Demo Owner",
        "Your dog’s devoted walking companion.",
        "I believe the best care begins by noticing the details: how your dog greets new people, which route helps them settle, what keeps them comfortable, and when they need a little extra patience.\n\nMy approach is calm, dependable, and personal. I work with each family to understand routines, health needs, behavior, and the small preferences that make a walk or visit feel familiar.\n\nPrincess Dog Walker currently operates exclusively in the Demo City, Florida area, providing dependable local care for nearby dogs and their families.",
        "202-555-0100", "owner@example.test", "@demo_petcare",
        "http://localhost:5095/contact.html", "Demo City, Florida",
        "http://localhost:5095/contact.html",
        "Send Demo Owner a message");

    private static readonly Dictionary<string, Func<OwnerProfileContentRequest, string>> OwnerProfileFields = new()
    {
        ["owner-section-label"] = profile => profile.SectionLabel,
        ["owner-section-title"] = profile => profile.SectionTitle,
        ["owner-greeting"] = profile => profile.Greeting,
        ["owner-headline"] = profile => profile.Headline,
        ["owner-biography"] = profile => profile.Biography,
        ["owner-phone"] = profile => profile.Phone,
        ["owner-public-email"] = profile => profile.Email,
        ["owner-instagram-label"] = profile => profile.InstagramLabel,
        ["owner-instagram-url"] = profile => profile.InstagramUrl,
        ["owner-service-area"] = profile => profile.ServiceArea,
        ["owner-map-url"] = profile => profile.MapUrl,
        ["owner-contact-button"] = profile => profile.ContactButtonText
    };

    [HttpGet("about-photo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAboutPhoto(CancellationToken cancellationToken)
    {
        var photo = await db.SiteContent.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == AboutPhotoKey, cancellationToken);
        if (photo is null) return NotFound();

        var etag = $"\"{photo.UpdatedAt.UtcTicks:x}\"";
        if (Request.Headers.IfNoneMatch.ToString() == etag)
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Data, photo.ContentType);
    }

    [HttpGet("owner-description"), AllowAnonymous]
    public async Task<IActionResult> GetOwnerDescription(CancellationToken cancellationToken)
    {
        var content = await db.SiteContent.AsNoTracking().SingleOrDefaultAsync(
            item => item.Key == OwnerDescriptionKey, cancellationToken);
        return Ok(new { text = content?.Text ?? "Hi, I’m Demo Owner. I provide thoughtful, dependable care tailored to every dog and family." });
    }

    [HttpPut("owner-description"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> UpdateOwnerDescription([FromBody] OwnerDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length is < 1 or > 3000) return BadRequest(new { message = "Description must be between 1 and 3,000 characters." });
        var content = await db.SiteContent.SingleOrDefaultAsync(item => item.Key == OwnerDescriptionKey, cancellationToken);
        if (content is null) { content = new SiteContent { Key = OwnerDescriptionKey }; db.SiteContent.Add(content); }
        content.Text = text;
        content.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { content.Text, content.UpdatedAt });
    }

    [HttpGet("owner-profile"), AllowAnonymous]
    public async Task<ActionResult<OwnerProfileContentRequest>> GetOwnerProfile(CancellationToken cancellationToken)
    {
        var keys = OwnerProfileFields.Keys.Append(OwnerDescriptionKey).ToArray();
        var saved = await db.SiteContent.AsNoTracking()
            .Where(item => keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Text, cancellationToken);
        return Ok(new OwnerProfileContentRequest(
            Value("owner-section-label", DefaultOwnerProfile.SectionLabel),
            Value("owner-section-title", DefaultOwnerProfile.SectionTitle),
            Value("owner-greeting", DefaultOwnerProfile.Greeting),
            Value("owner-headline", DefaultOwnerProfile.Headline),
            Value("owner-biography", Value(OwnerDescriptionKey, DefaultOwnerProfile.Biography)),
            Value("owner-phone", DefaultOwnerProfile.Phone),
            Value("owner-public-email", DefaultOwnerProfile.Email),
            Value("owner-instagram-label", DefaultOwnerProfile.InstagramLabel),
            Value("owner-instagram-url", DefaultOwnerProfile.InstagramUrl),
            Value("owner-service-area", DefaultOwnerProfile.ServiceArea),
            Value("owner-map-url", DefaultOwnerProfile.MapUrl),
            Value("owner-contact-button", DefaultOwnerProfile.ContactButtonText)));

        string Value(string key, string fallback) =>
            saved.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    [HttpPut("owner-profile"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<OwnerProfileContentRequest>> UpdateOwnerProfile(
        OwnerProfileContentRequest request, CancellationToken cancellationToken)
    {
        if (!IsHttpsUrl(request.InstagramUrl) || !IsHttpsUrl(request.MapUrl))
            return BadRequest(new { message = "Instagram and map links must be valid HTTPS addresses." });

        var values = OwnerProfileFields.ToDictionary(field => field.Key,
            field => field.Value(request).Trim());
        var keys = values.Keys.Append(OwnerDescriptionKey).ToArray();
        var existing = await db.SiteContent.Where(item => keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, cancellationToken);
        foreach (var (key, value) in values)
        {
            if (!existing.TryGetValue(key, out var content))
            {
                content = new SiteContent { Key = key };
                db.SiteContent.Add(content);
            }
            content.Text = value;
            content.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (!existing.TryGetValue(OwnerDescriptionKey, out var description))
        {
            description = new SiteContent { Key = OwnerDescriptionKey };
            db.SiteContent.Add(description);
        }
        description.Text = request.Biography.Trim();
        description.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(request);
    }

    [HttpPut("about-photo")]
    [Authorize(Roles = AppRoles.Owner)]
    [RequestSizeLimit(MaximumPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> UpdateAboutPhoto(
        [FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { message = "Choose a photo to upload." });
        if (photo.Length > MaximumPhotoBytes)
            return BadRequest(new { message = "The photo must be 5 MB or smaller." });

        await using var stream = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        var contentType = DetectImageType(bytes);
        if (contentType is null)
            return BadRequest(new { message = "Use a JPEG, PNG, or WebP photo." });

        var saved = await db.SiteContent.SingleOrDefaultAsync(
            item => item.Key == AboutPhotoKey, cancellationToken);
        if (saved is null)
        {
            saved = new SiteContent { Key = AboutPhotoKey };
            db.SiteContent.Add(saved);
        }

        saved.Data = bytes;
        saved.ContentType = contentType;
        saved.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { updatedAt = saved.UpdatedAt });
    }

    private static string? DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return "image/png";
        if (bytes.Length >= 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

public record OwnerDescriptionRequest(string? Text);

public record OwnerProfileContentRequest(
    [Required, MaxLength(120)] string SectionLabel,
    [Required, MaxLength(180)] string SectionTitle,
    [Required, MaxLength(100)] string Greeting,
    [Required, MaxLength(180)] string Headline,
    [Required, MaxLength(3000)] string Biography,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(120)] string InstagramLabel,
    [Required, Url, MaxLength(500)] string InstagramUrl,
    [Required, MaxLength(160)] string ServiceArea,
    [Required, Url, MaxLength(500)] string MapUrl,
    [Required, MaxLength(100)] string ContactButtonText);

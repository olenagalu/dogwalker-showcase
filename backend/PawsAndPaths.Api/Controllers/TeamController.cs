using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Route("api/team")]
public class TeamController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(
        await db.TeamMembers.AsNoTracking().OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.Role, item.Bio, HasPhoto = item.PhotoData.Length > 0 })
            .ToListAsync(cancellationToken));

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var item = await db.TeamMembers.AsNoTracking().SingleOrDefaultAsync(member => member.Id == id, cancellationToken);
        if (item is null || item.PhotoData.Length == 0) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(item.PhotoData, item.PhotoContentType);
    }

    [HttpPost, Authorize(Roles = AppRoles.Owner), RequestSizeLimit(ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] string? role,
        [FromForm] string? bio, [FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        if (!Valid(name, role, bio))
            return BadRequest(new { message = "Enter a name and keep the role and biography within their limits." });
        var member = new TeamMember { Name = name.Trim(), Role = (role ?? string.Empty).Trim(), Bio = (bio ?? string.Empty).Trim() };
        if (photo is not null)
        {
            var upload = await ImageUpload.ReadAsync(photo, cancellationToken);
            if (upload.Error is not null) return BadRequest(new { message = upload.Error });
            member.PhotoData = upload.Data!; member.PhotoContentType = upload.ContentType!;
        }
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { member.Id, member.Name, member.Role, member.Bio, HasPhoto = member.PhotoData.Length > 0 });
    }

    [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Owner), RequestSizeLimit(ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> Update(int id, [FromForm] string name, [FromForm] string? role,
        [FromForm] string? bio, [FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        if (!Valid(name, role, bio))
            return BadRequest(new { message = "Enter a name and keep the role and biography within their limits." });
        var member = await db.TeamMembers.FindAsync([id], cancellationToken);
        if (member is null) return NotFound();
        member.Name = name.Trim();
        member.Role = (role ?? string.Empty).Trim();
        member.Bio = (bio ?? string.Empty).Trim();
        if (photo is not null)
        {
            var upload = await ImageUpload.ReadAsync(photo, cancellationToken);
            if (upload.Error is not null) return BadRequest(new { message = upload.Error });
            member.PhotoData = upload.Data!;
            member.PhotoContentType = upload.ContentType!;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { member.Id, member.Name, member.Role, member.Bio, HasPhoto = member.PhotoData.Length > 0 });
    }

    [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await db.TeamMembers.FindAsync([id], cancellationToken);
        if (item is null) return NotFound();
        db.TeamMembers.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool Valid(string name, string? role, string? bio) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 120
        && (role?.Length ?? 0) <= 120 && (bio?.Length ?? 0) <= 1500;
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Controllers;

[ApiController, Authorize(Roles = AppRoles.Customer + "," + AppRoles.Owner)]
[Route("api/dogs")]
public class DogsController(
    AppDbContext db,
    UserManager<AppUser> userManager,
    IOwnerNotificationEmailSender notificationSender,
    ILogger<DogsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DogDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok((await db.Dogs.AsNoTracking().Where(dog => dog.UserId == userId)
            .OrderBy(dog => dog.Name).ToListAsync(cancellationToken)).Select(dog => dog.ToDto()));
    }

    [HttpPost]
    public async Task<ActionResult<DogDto>> Create(CreateDogDto request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await userManager.HasPasswordAsync(user))
            return Conflict(new { message = "Set an account password in Security before registering a dog." });
        if (!await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "The account password is incorrect." });

        var dog = new Dog { UserId = user.Id, Name = request.Name.Trim() };
        Apply(dog, new DogWriteDto(request.Name, request.Breed, request.Age,
            request.CareInstructions, request.BehavioralNotes, request.MedicalNotes));
        db.Dogs.Add(dog);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await notificationSender.SendDogRegistrationAsync(new DogRegistrationNotification(
                user.FullName, user.Email ?? string.Empty, user.PhoneNumber ?? string.Empty,
                user.ServiceAddress, dog.Name, dog.Breed, dog.Age,
                dog.CareInstructions, dog.BehavioralNotes, dog.MedicalNotes), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Owner email notification failed for dog {DogId}.", dog.Id);
        }
        return CreatedAtAction(nameof(GetById), new { id = dog.Id }, dog.ToDto());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DogDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        return dog is null ? NotFound() : Ok(dog.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DogDto>> Update(int id, DogWriteDto request, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        if (dog is null) return NotFound();
        Apply(dog, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(dog.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        if (dog is null) return NotFound();
        if (await db.Bookings.AnyAsync(booking => booking.DogId == id, cancellationToken))
            return Conflict(new { message = "Dogs with booking history cannot be deleted." });
        db.Dogs.Remove(dog);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var dog = await FindOwnedOrOwner(id, cancellationToken);
        if (dog is null) return NotFound();
        if (dog.PhotoData.Length == 0) return NotFound();
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(dog.PhotoData, dog.PhotoContentType);
    }

    [HttpPut("{id:int}/photo"), RequestSizeLimit(ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> UpdatePhoto(int id, [FromForm] IFormFile? photo,
        CancellationToken cancellationToken)
    {
        var dog = await FindOwned(id, cancellationToken);
        if (dog is null) return NotFound();
        var upload = await ImageUpload.ReadAsync(photo, cancellationToken);
        if (upload.Error is not null) return BadRequest(new { message = upload.Error });
        dog.PhotoData = upload.Data!;
        dog.PhotoContentType = upload.ContentType!;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<Dog?> FindOwned(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return db.Dogs.SingleOrDefaultAsync(dog => dog.Id == id && dog.UserId == userId, cancellationToken);
    }

    private Task<Dog?> FindOwnedOrOwner(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isOwner = User.IsInRole(AppRoles.Owner);
        return db.Dogs.SingleOrDefaultAsync(dog => dog.Id == id && (isOwner || dog.UserId == userId), cancellationToken);
    }

    private static void Apply(Dog dog, DogWriteDto request)
    {
        dog.Name = request.Name.Trim();
        dog.Breed = request.Breed?.Trim() ?? string.Empty;
        dog.Age = request.Age;
        dog.CareInstructions = request.CareInstructions?.Trim() ?? string.Empty;
        dog.BehavioralNotes = request.BehavioralNotes?.Trim() ?? string.Empty;
        dog.MedicalNotes = request.MedicalNotes?.Trim() ?? string.Empty;
    }
}

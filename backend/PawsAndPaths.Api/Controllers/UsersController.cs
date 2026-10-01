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

[ApiController, Authorize]
[Route("api/users")]
public class UsersController(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICustomerManagementService customerManagementService,
    IAccountDecisionEmailSender accountDecisionEmailSender,
    IOwnerNotificationEmailSender ownerNotificationEmailSender,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.Owner) ? AppRoles.Owner : roles.Contains(AppRoles.Assistant) ? AppRoles.Assistant : AppRoles.Customer;
        return Ok(new UserProfileDto(user.Id, user.FullName, user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty, role, user.ServiceArea, user.ServiceAddress,
            user.ApprovalStatus, user.ProfilePhotoData.Length > 0));
    }

    [HttpPut("me"), Authorize(Roles = AppRoles.Customer + "," + AppRoles.Owner)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileDto request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var areaChanged = !string.Equals(user.ServiceArea, request.ServiceArea.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.ServiceAddress, request.ServiceAddress.Trim(), StringComparison.OrdinalIgnoreCase);
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.Phone.Trim();
        user.ServiceArea = request.ServiceArea.Trim();
        user.ServiceAddress = request.ServiceAddress.Trim();
        var needsApprovalNotification = areaChanged && !await userManager.IsInRoleAsync(user, AppRoles.Owner);
        if (needsApprovalNotification)
            user.ApprovalStatus = AccountApprovalStatus.Pending;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);
        if (needsApprovalNotification && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                await ownerNotificationEmailSender.SendAccountApprovalRequestAsync(
                    new AccountApprovalNotification(user.FullName, user.Email,
                        user.PhoneNumber ?? string.Empty, user.ServiceArea, user.ServiceAddress));
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Owner account-approval notification could not be sent for user {UserId}.", user.Id);
            }
        }
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.Owner) ? AppRoles.Owner : roles.Contains(AppRoles.Assistant) ? AppRoles.Assistant : AppRoles.Customer;
        return Ok(new UserProfileDto(user.Id, user.FullName, user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty, role, user.ServiceArea, user.ServiceAddress,
            user.ApprovalStatus, user.ProfilePhotoData.Length > 0));
    }

    [HttpGet("customers"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryDto>>> Customers(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Include(user => user.Dogs)
            .OrderBy(user => user.FullName).ToListAsync(cancellationToken);
        var customers = new List<CustomerSummaryDto>();
        foreach (var user in users)
        {
            if (await userManager.IsInRoleAsync(user, AppRoles.Customer))
                customers.Add(new CustomerSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty, user.ServiceArea, user.ServiceAddress,
                    user.ApprovalStatus, user.ProfilePhotoData.Length > 0,
                    user.Dogs.Select(dog => dog.ToDto()).ToList()));
        }
        return Ok(customers);
    }

    [HttpPost("customers-with-dog"), Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<CustomerSummaryDto>> CreateCustomerWithDog(
        CreateOwnerCustomerWithDogDto request, CancellationToken cancellationToken)
    {
        var (customer, error) = await customerManagementService.CreateWithDogAsync(request, cancellationToken);
        return customer is null
            ? Conflict(new { message = error })
            : CreatedAtAction(nameof(Customers), new { id = customer.Id }, customer);
    }

    [HttpPut("customers/{id}/approval"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> UpdateApproval(string id, UpdateApprovalDto request,
        CancellationToken cancellationToken)
    {
        if (request.Status == AccountApprovalStatus.Pending)
            return BadRequest(new { message = "Choose Approved or Declined." });
        var customer = await userManager.FindByIdAsync(id);
        if (customer is null || !await userManager.IsInRoleAsync(customer, AppRoles.Customer)) return NotFound();
        if (request.Status == AccountApprovalStatus.Approved
            && (string.IsNullOrWhiteSpace(customer.ServiceArea) || string.IsNullOrWhiteSpace(customer.ServiceAddress)))
            return Conflict(new { message = "The customer must provide a service area and address before approval." });
        var sendDecisionEmail = request.Status != customer.ApprovalStatus;
        customer.ApprovalStatus = request.Status;
        var update = await userManager.UpdateAsync(customer);
        if (!update.Succeeded) return BadRequest(update.Errors);
        if (sendDecisionEmail && request.Status == AccountApprovalStatus.Declined
            && !string.IsNullOrWhiteSpace(customer.Email))
        {
            try
            {
                await accountDecisionEmailSender.SendDeclinedAsync(
                    customer.Email, customer.FullName, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Account-decision email could not be sent for user {UserId}.", customer.Id);
            }
        }
        return NoContent();
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await userManager.HasPasswordAsync(user))
            return Conflict(new { message = "This account uses Google sign-in and does not have a password yet." });
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? NoContent() : ValidationProblem(new ValidationProblemDetails(
            result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key,
                group => group.Select(error => error.Description).ToArray())));
    }

    [HttpGet("me/photo")]
    public async Task<IActionResult> MyPhoto()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Unauthorized() : Photo(user);
    }

    [HttpGet("{id}/photo"), Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> CustomerPhoto(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        return user is null ? NotFound() : Photo(user);
    }

    [HttpPut("me/photo"), RequestSizeLimit(PawsAndPaths.Api.Services.ImageUpload.MaximumBytes + 65536)]
    public async Task<IActionResult> UpdateMyPhoto([FromForm] IFormFile? photo, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var upload = await PawsAndPaths.Api.Services.ImageUpload.ReadAsync(photo, cancellationToken);
        if (upload.Error is not null) return BadRequest(new { message = upload.Error });
        user.ProfilePhotoData = upload.Data!;
        user.ProfilePhotoContentType = upload.ContentType!;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private IActionResult Photo(AppUser user)
    {
        if (user.ProfilePhotoData.Length == 0) return NotFound();
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(user.ProfilePhotoData, user.ProfilePhotoContentType);
    }
}

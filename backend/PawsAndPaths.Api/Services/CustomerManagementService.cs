using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface ICustomerManagementService
{
    Task<(CustomerSummaryDto? Customer, string? Error)> CreateWithDogAsync(
        CreateOwnerCustomerWithDogDto request, CancellationToken cancellationToken);
}

public sealed class CustomerManagementService(AppDbContext db, UserManager<AppUser> userManager)
    : ICustomerManagementService
{
    public async Task<(CustomerSummaryDto? Customer, string? Error)> CreateWithDogAsync(
        CreateOwnerCustomerWithDogDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            return (null, "A customer with this email already exists.");

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone.Trim(),
            ServiceArea = request.ServiceArea.Trim(),
            ServiceAddress = request.ServiceAddress.Trim(),
            ApprovalStatus = AccountApprovalStatus.Approved
        };
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return (null, string.Join(" ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return (null, string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        var dog = new Dog
        {
            UserId = user.Id,
            User = user,
            Name = request.DogName.Trim(),
            Breed = request.DogBreed?.Trim() ?? string.Empty,
            Age = request.DogAge
        };
        db.Dogs.Add(dog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        return (new CustomerSummaryDto(user.Id, user.FullName, email,
            user.PhoneNumber ?? string.Empty, user.ServiceArea, user.ServiceAddress,
            user.ApprovalStatus, false, [dog.ToDto()]), null);
    }
}

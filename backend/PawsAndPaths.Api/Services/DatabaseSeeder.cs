using Microsoft.AspNetCore.Identity;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public static class DatabaseSeeder
{
    public static async Task SeedIdentityAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Customer, AppRoles.Owner, AppRoles.Assistant })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var ownerEmail = configuration["Owner:Email"];
        var ownerPassword = configuration["Owner:Password"];
        if (string.IsNullOrWhiteSpace(ownerEmail) || string.IsNullOrWhiteSpace(ownerPassword)) return;

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var owner = await userManager.FindByEmailAsync(ownerEmail);
        if (owner is null)
        {
            owner = new AppUser { FullName = "Princess Dog Walker", UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(owner, ownerPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
        if (!await userManager.IsInRoleAsync(owner, AppRoles.Owner)) await userManager.AddToRoleAsync(owner, AppRoles.Owner);
        if (await userManager.IsInRoleAsync(owner, AppRoles.Customer)) await userManager.RemoveFromRoleAsync(owner, AppRoles.Customer);
    }
}

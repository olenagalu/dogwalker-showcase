using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        foreach (var (email, name, role, password) in new[] {
            ("customer@example.test", "Demo Customer", AppRoles.Customer, "DemoCustomer123!"),
            ("assistant@example.test", "Demo Assistant", AppRoles.Assistant, "DemoAssistant123!") })
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser { UserName = email, Email = email, FullName = name,
                    EmailConfirmed = true, PhoneNumber = "202-555-0100",
                    ServiceArea = "Demo City", ServiceAddress = "123 Demo Street",
                    ApprovalStatus = AccountApprovalStatus.Approved,
                    AssistantStatus = role == AppRoles.Assistant ? AssistantAccountStatus.Active : null };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded) throw new InvalidOperationException("Could not create demo account.");
            }
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
            if (role == AppRoles.Customer && !await db.Dogs.AnyAsync(d => d.UserId == user.Id))
                db.Dogs.Add(new Dog { UserId = user.Id, Name = "Demo Mochi", Breed = "Fictional mixed breed", Age = 3 });
        }
        await db.SaveChangesAsync();
    }
}

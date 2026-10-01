using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class CustomerManagementServiceTests
{
    [Fact]
    public async Task OwnerCanCreatePasswordlessCustomerWithDog()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole(AppRoles.Customer));
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var service = new CustomerManagementService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(), userManager);

        var (customer, error) = await service.CreateWithDogAsync(
            new CreateOwnerCustomerWithDogDto(
                "Morgan Lee", "MORGAN@example.test", "202-555-0101",
                "Demo City", "123 Palm Ave", "Luna", "Mixed breed", 4),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(customer);
        Assert.Equal("morgan@example.test", customer.Email);
        Assert.Equal("Luna", Assert.Single(customer.Dogs).Name);
        var user = await userManager.FindByEmailAsync("morgan@example.test");
        Assert.NotNull(user);
        Assert.False(await userManager.HasPasswordAsync(user));
        Assert.True(await userManager.IsInRoleAsync(user, AppRoles.Customer));
        Assert.Equal(AccountApprovalStatus.Approved, user.ApprovalStatus);
    }

    [Fact]
    public void CreateCustomerWithDogEndpoint_IsOwnerOnly()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.CreateCustomerWithDog));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AppRoles.Owner, authorize.Roles);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<AppUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }
}

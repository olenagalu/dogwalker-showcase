using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Tests;

public class ContactControllerTests
{
    [Fact]
    public void ReadingCustomerRequests_IsOwnerOnly()
    {
        var method = typeof(ContactController).GetMethod(nameof(ContactController.GetAll));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AppRoles.Owner, authorize.Roles);
    }
}

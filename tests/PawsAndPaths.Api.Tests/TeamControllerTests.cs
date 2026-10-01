using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Tests;

public class TeamControllerTests
{
    [Fact]
    public void Update_IsOwnerOnly()
    {
        var method = typeof(TeamController).GetMethod(nameof(TeamController.Update));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AppRoles.Owner, authorize.Roles);
    }

    [Fact]
    public async Task Update_ChangesPublicDetails_AndPreservesExistingPhoto()
    {
        await using var db = CreateDatabase();
        var member = new TeamMember
        {
            Name = "Taylor", Role = "Walker", Bio = "Original biography",
            PhotoData = [0xff, 0xd8, 0xff], PhotoContentType = "image/jpeg"
        };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var result = await Controller(db).Update(member.Id, "Taylor Smith", "Pet-care specialist",
            "Updated biography", null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.TeamMembers.SingleAsync();
        Assert.Equal("Taylor Smith", saved.Name);
        Assert.Equal("Pet-care specialist", saved.Role);
        Assert.Equal("Updated biography", saved.Bio);
        Assert.Equal([0xff, 0xd8, 0xff], saved.PhotoData);
    }

    private static TeamController Controller(AppDbContext db) => new(db)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

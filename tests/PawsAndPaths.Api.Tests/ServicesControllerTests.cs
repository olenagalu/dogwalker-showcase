using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Tests;

public class ServicesControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsActiveServicesFromLowestToHighestPrice()
    {
        await using var db = CreateDatabase();
        db.Services.AddRange(
            Service("Premium visit", 65m),
            Service("Quick walk", 18m),
            Service("Standard walk", 30m),
            Service("Hidden service", 5m, false));
        await db.SaveChangesAsync();

        var controller = Controller(db);
        var response = await controller.GetAll();
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var services = Assert.IsAssignableFrom<IEnumerable<ServiceDto>>(result.Value).ToList();

        Assert.Equal([18m, 30m, 65m], services.Select(service => service.Price));
    }

    [Fact]
    public async Task OwnerServiceWorkflow_CanCreateAndUpdateAService()
    {
        await using var db = CreateDatabase();
        var controller = Controller(db);

        var createdResponse = await controller.Create(
            new ServiceWriteDto("Puppy visit", "A gentle puppy check-in.", 25, 22m, true),
            CancellationToken.None);
        var createdResult = Assert.IsType<CreatedResult>(createdResponse.Result);
        var created = Assert.IsType<ServiceDto>(createdResult.Value);

        var updatedResponse = await controller.Update(created.Id,
            new ServiceWriteDto("Puppy visit plus", "A longer puppy check-in.", 40, 34m, true),
            CancellationToken.None);
        var updatedResult = Assert.IsType<OkObjectResult>(updatedResponse.Result);
        var updated = Assert.IsType<ServiceDto>(updatedResult.Value);

        Assert.Equal("Puppy visit plus", updated.Name);
        Assert.Equal(34m, updated.Price);
        Assert.Equal(40, updated.DurationMinutes);
    }

    private static ServicesController Controller(AppDbContext db) => new(db)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ServiceOffering Service(string name, decimal price, bool active = true) => new()
    {
        Name = name,
        Description = $"{name} description",
        DurationMinutes = 30,
        Price = price,
        IsActive = active
    };
}

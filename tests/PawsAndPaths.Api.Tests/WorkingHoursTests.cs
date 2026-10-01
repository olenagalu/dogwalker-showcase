using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class WorkingHoursTests
{
    [Fact]
    public async Task SavedHoursControlSlotsAndBookingValidation()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hours = new WorkingHours(new(9,0),new(11,0),new(23,0),new(6,0),35,true);
        db.SiteContent.Add(new SiteContent { Key=WorkingHours.Key, Text=System.Text.Json.JsonSerializer.Serialize(hours) });
        db.Services.Add(new ServiceOffering {Id=1, Name="Walk", Description="Walk", DurationMinutes=60, IsActive=true});
        await db.SaveChangesAsync();
        var day=DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var availability=new AvailabilityService(db);
        var slots=await availability.GetSlotsAsync(day,day,1,default);
        Assert.Equal(new TimeOnly(9,0), slots.First().StartTime);
        Assert.Equal(new TimeOnly(10,0), slots.Last().StartTime);
        Assert.False(await availability.IsAvailableAsync(day,new(8,0),new(9,0),null,default));
        Assert.True(await availability.IsAvailableAsync(day,new(9,0),new(10,0),null,default));
        Assert.True(hours.IsEmergency(new(23,30)));
        Assert.True(hours.IsEmergency(new(2,0)));
        Assert.False(hours.IsEmergency(new(6,0)));
        Assert.False(hours.IsEmergency(new(12,0)));
    }
}

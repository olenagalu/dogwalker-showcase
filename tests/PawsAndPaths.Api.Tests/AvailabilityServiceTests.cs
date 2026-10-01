using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task DaySchedule_DistinguishesBookedBlockedAndBookableTimeWithoutPrivateData()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "Drop-in visit", Description = "Pet care",
            DurationMinutes = 30, Price = 22m, IsActive = true
        };
        db.Services.Add(service);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date, StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0), IsAvailable = false
        });
        db.Bookings.Add(new Booking
        {
            UserId = "private-user", DogId = 99, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0),
            Price = 22m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var schedule = await new AvailabilityService(db)
            .GetDayScheduleAsync(date, service.Id, CancellationToken.None);

        Assert.Equal(48, schedule.Count);
        Assert.Equal("Booked", schedule.Single(item => item.StartTime == new TimeOnly(10, 0)).Status);
        Assert.Equal("Unavailable", schedule.Single(item => item.StartTime == new TimeOnly(14, 0)).Status);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(12, 0)).IsBookable);
        Assert.All(schedule, item =>
            Assert.DoesNotContain("private", $"{item.StartTime}{item.Status}{item.IsBookable}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DaySchedule_DoesNotOfferAStartThatWouldOverlapABooking()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "One-hour visit", Description = "Pet care",
            DurationMinutes = 60, Price = 35m, IsActive = true
        };
        db.Services.Add(service);
        db.Bookings.Add(new Booking
        {
            UserId = "user", DogId = 1, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30),
            Price = 35m, Status = BookingStatus.Pending
        });
        await db.SaveChangesAsync();

        var schedule = await new AvailabilityService(db)
            .GetDayScheduleAsync(date, service.Id, CancellationToken.None);

        Assert.False(schedule.Single(item => item.StartTime == new TimeOnly(9, 30)).IsBookable);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(10, 30)).IsBookable);
    }

    [Fact]
    public async Task Slots_BulkCalendarQueryRespectsBookingsAndBlocks()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "Drop-in visit", Description = "Pet care",
            DurationMinutes = 30, Price = 22m, IsActive = true
        };
        db.Services.Add(service);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date.AddDays(1), StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0), IsAvailable = false
        });
        db.Bookings.Add(new Booking
        {
            UserId = "user", DogId = 1, ServiceOfferingId = service.Id,
            Date = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30),
            Price = 22m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var slots = await new AvailabilityService(db)
            .GetSlotsAsync(date, date.AddDays(1), service.Id, CancellationToken.None);

        Assert.DoesNotContain(slots, item => item.Date == date && item.StartTime == new TimeOnly(10, 0));
        Assert.DoesNotContain(slots, item => item.Date == date.AddDays(1) && item.StartTime == new TimeOnly(14, 0));
        Assert.Contains(slots, item => item.Date == date && item.StartTime == new TimeOnly(10, 30));
    }

    [Fact]
    public async Task RegularServiceSlots_RunFromSevenAmAndFinishByElevenPm()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 1, Name = "Drop-in visit", Description = "Pet care",
            DurationMinutes = 30, Price = 22m, IsActive = true
        };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var availability = new AvailabilityService(db);
        var slots = await availability.GetSlotsAsync(date, date, service.Id, CancellationToken.None);
        var schedule = await availability.GetDayScheduleAsync(date, service.Id, CancellationToken.None);

        Assert.Equal(new TimeOnly(7, 0), slots.First().StartTime);
        Assert.Equal(new TimeOnly(22, 30), slots.Last().StartTime);
        Assert.False(schedule.Single(item => item.StartTime == new TimeOnly(5, 30)).IsBookable);
        Assert.False(schedule.Single(item => item.StartTime == new TimeOnly(6, 0)).IsBookable);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(7, 0)).IsBookable);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(22, 30)).IsBookable);
        Assert.False(schedule.Single(item => item.StartTime == new TimeOnly(23, 0)).IsBookable);
    }

    [Fact]
    public async Task OvernightCareWindows_CanRunOutsideRegularHours()
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var availability = new AvailabilityService(db);

        var regularAvailable = await availability.IsAvailableAsync(
            date, new TimeOnly(23, 0), new TimeOnly(23, 30), null, CancellationToken.None);
        var overnightAvailable = await availability.IsAvailableAsync(
            date, new TimeOnly(23, 0), new TimeOnly(23, 30), null, CancellationToken.None, false);
        var overnightMorningAvailable = await availability.IsAvailableAsync(
            date, TimeOnly.MinValue, new TimeOnly(9, 0), null, CancellationToken.None, false);

        Assert.False(regularAvailable);
        Assert.True(overnightAvailable);
        Assert.True(overnightMorningAvailable);
    }

    [Fact]
    public async Task OvernightDateCheck_ReturnsOnlyConflictedDatesWithoutPrivateDetails()
    {
        await using var db = CreateDatabase();
        var checkIn = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var service = new ServiceOffering
        {
            Id = 4, Name = "Overnight stay", Description = "Multi-day care",
            DurationMinutes = 660, Price = 95m, IsActive = true, IsOvernightStay = true
        };
        db.Services.Add(service);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = checkIn.AddDays(1), StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0), IsAvailable = false, Notes = "Private appointment"
        });
        await db.SaveChangesAsync();

        var result = await new AvailabilityService(db).CheckOvernightAsync(
            checkIn, checkIn.AddDays(2), service.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsAvailable);
        Assert.Equal([checkIn.AddDays(1)], result.UnavailableDates);
        Assert.DoesNotContain("Private", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmergencyHours_AppearOnlyWhenEnabledAndRespectConflicts(bool enabled)
    {
        await using var db = CreateDatabase();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        db.Services.Add(new ServiceOffering
        {
            Id = 1, Name = "Visit", Description = "Care", DurationMinutes = 30,
            Price = 22m, IsActive = true
        });
        db.SiteContent.Add(new SiteContent
        {
            Key = WorkingHours.Key,
            Text = System.Text.Json.JsonSerializer.Serialize(
                new WorkingHours(new(7, 0), new(23, 0), new(23, 0), new(7, 0), 45m, enabled))
        });
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date, StartTime = new(1, 0), EndTime = new(2, 0), IsAvailable = false
        });
        db.Bookings.Add(new Booking
        {
            UserId = "private-user", DogId = 1, ServiceOfferingId = 1,
            Date = date, StartTime = new(3, 0), EndTime = new(4, 0),
            Price = 22m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();
        var availability = new AvailabilityService(db);
        var schedule = await availability.GetDayScheduleAsync(date, 1, CancellationToken.None);
        foreach (var time in new[] { new TimeOnly(0, 0), new TimeOnly(6, 30), new TimeOnly(23, 0), new TimeOnly(23, 30) })
        {
            var segment = schedule.Single(item => item.StartTime == time);
            Assert.Equal(enabled ? "Emergency" : "Unavailable", segment.Status);
            Assert.False(segment.IsBookable);
        }
        Assert.Equal("Unavailable", schedule.Single(item => item.StartTime == new TimeOnly(1, 0)).Status);
        Assert.Equal("Booked", schedule.Single(item => item.StartTime == new TimeOnly(3, 0)).Status);
        Assert.True(schedule.Single(item => item.StartTime == new TimeOnly(7, 0)).IsBookable);
        var past = await availability.GetDayScheduleAsync(date.AddDays(-10), 1, CancellationToken.None);
        Assert.DoesNotContain(past, item => item.Status == "Emergency");
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_UsesServiceDurationAndPrice()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), "Use the pink leash."),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
        Assert.Equal(new TimeOnly(10, 30), booking.EndTime);
        Assert.Equal(24m, booking.Price);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task CreateBooking_PreventsOverlappingActiveBooking()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));
        await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        var secondUser = new AppUser
        {
            Id = "user-2", UserName = "second@example.test", Email = "second@example.test",
            FullName = "Second Customer", ServiceArea = "Demo City",
            ServiceAddress = "200 Palm Ave"
        };
        var secondDog = new Dog { UserId = secondUser.Id, User = secondUser, Name = "Scout" };
        db.Users.Add(secondUser);
        db.Dogs.Add(secondDog);
        await db.SaveChangesAsync();
        var (booking, error) = await bookingService.CreateAsync(secondUser.Id,
            new CreateBookingDto(secondDog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        Assert.Null(booking);
        Assert.Equal("That time is no longer available.", error);
        Assert.Single(await db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_AllowsAnyOpenTimeWithoutAvailabilityRules()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(21, 30), null),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
    }

    [Fact]
    public async Task CreateBooking_RejectsOwnerBlockedTime()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        db.Availability.Add(new AvailabilityRule
        {
            SpecificDate = date,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(16, 0),
            IsAvailable = false
        });
        await db.SaveChangesAsync();
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(15, 0), null),
            CancellationToken.None);

        Assert.Null(booking);
        Assert.Equal("That time is no longer available.", error);
    }

    [Fact]
    public async Task OwnerCreatedBooking_CanStartConfirmed()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        var bookingService = new BookingService(db, new AvailabilityService(db));

        var (booking, error) = await bookingService.CreateAsync(user.Id,
            new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(18, 0), "Booked by Demo Owner."),
            CancellationToken.None, BookingStatus.Confirmed);

        Assert.Null(error);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task OvernightSchedule_ShowsThreeCareWindowsOnMiddleDays()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var booking = new Booking
        {
            UserId = "user", DogId = 1, ServiceOfferingId = 1,
            Date = start, EndDate = start.AddDays(2), IsOvernightStay = true,
            StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(9, 0),
            OvernightStartTime = new TimeOnly(22, 0), OvernightEndTime = new TimeOnly(9, 0),
            MiddayStartTime = new TimeOnly(14, 0), MiddayEndTime = new TimeOnly(15, 0)
        };

        var middle = BookingSchedule.Windows(booking).Where(window => window.Date == start.AddDays(1)).ToList();

        Assert.Equal(3, middle.Count);
        Assert.Equal(new TimeOnly(9, 0), middle[0].EndTime);
        Assert.Equal(new TimeOnly(14, 0), middle[1].StartTime);
        Assert.Equal(new TimeOnly(22, 0), middle[2].StartTime);
    }

    [Fact]
    public async Task OvernightMiddayVisit_BlocksRegularBookingSlot()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        db.Bookings.Add(new Booking
        {
            UserId = user.Id, User = user, DogId = dog.Id, Dog = dog,
            ServiceOfferingId = service.Id, ServiceOffering = service,
            Date = date, EndDate = date.AddDays(2), IsOvernightStay = true,
            StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(9, 0),
            OvernightStartTime = new TimeOnly(22, 0), OvernightEndTime = new TimeOnly(9, 0),
            MiddayStartTime = new TimeOnly(14, 0), MiddayEndTime = new TimeOnly(15, 0),
            Price = 100m, Status = BookingStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var available = await new AvailabilityService(db).IsAvailableAsync(
            date.AddDays(1), new TimeOnly(14, 0), new TimeOnly(14, 30), null, CancellationToken.None);

        Assert.False(available);
    }

    [Fact]
    public async Task OvernightBooking_MultipliesNightlyRateByNumberOfNights()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        service.IsOvernightStay = true;
        service.Price = 80m;
        await db.SaveChangesAsync();

        var (booking, error) = await new BookingService(db, new AvailabilityService(db)).CreateAsync(
            user.Id, new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(22, 0), null, date.AddDays(10)),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
        Assert.Equal(800m, booking.Price);
    }

    [Fact]
    public async Task PendingCustomer_CanCreatePendingBooking()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        user.ApprovalStatus = AccountApprovalStatus.Pending;
        await db.SaveChangesAsync();

        var (booking, error) = await new BookingService(db, new AvailabilityService(db)).CreateAsync(
            user.Id, new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task PendingCustomer_BookingCannotBeConfirmedUntilAccountIsApproved()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        user.ApprovalStatus = AccountApprovalStatus.Pending;
        await db.SaveChangesAsync();
        var bookingService = new BookingService(db, new AvailabilityService(db));
        var (booking, createError) = await bookingService.CreateAsync(
            user.Id, new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        var (confirmed, confirmError) = await bookingService.ChangeStatusAsync(
            booking!.Id, BookingStatus.Confirmed, CancellationToken.None);

        Assert.Null(createError);
        Assert.Null(confirmed);
        Assert.Contains("Approve", confirmError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeclinedCustomer_CannotCreateBooking()
    {
        await using var db = CreateDatabase();
        var (user, dog, service, date) = await Seed(db);
        user.ApprovalStatus = AccountApprovalStatus.Declined;
        await db.SaveChangesAsync();

        var (booking, error) = await new BookingService(db, new AvailabilityService(db)).CreateAsync(
            user.Id, new CreateBookingDto(dog.Id, service.Id, date, new TimeOnly(10, 0), null), CancellationToken.None);

        Assert.Null(booking);
        Assert.Contains("not available", error, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(AppUser User, Dog Dog, ServiceOffering Service, DateOnly Date)> Seed(AppDbContext db)
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var user = new AppUser
        {
            Id = "user-1", UserName = "sam@example.test", Email = "sam@example.test",
            FullName = "Sam Taylor", ServiceArea = "Demo City",
            ServiceAddress = "100 Palm Ave"
        };
        var dog = new Dog { UserId = user.Id, User = user, Name = "Mabel", Breed = "Beagle mix" };
        var service = new ServiceOffering { Id = 101, Name = "30-minute dog walk", Description = "A happy walk", DurationMinutes = 30, Price = 24m, IsActive = true };
        db.Users.Add(user);
        db.Dogs.Add(dog);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return (user, dog, service, date);
    }
}

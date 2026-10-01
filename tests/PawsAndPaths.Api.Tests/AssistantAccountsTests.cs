using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PawsAndPaths.Api.Controllers;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.DTOs;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class AssistantAccountsTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public ServiceProvider Provider { get; }
        public AppDbContext Db => Provider.GetRequiredService<AppDbContext>();
        public UserManager<AppUser> Users => Provider.GetRequiredService<UserManager<AppUser>>();
        public FakeEmail Email { get; } = new();
        public Fixture()
        {
            var services = new ServiceCollection().AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<AppUser>(o => { o.Password.RequireNonAlphanumeric = false; o.Password.RequireUppercase = false; })
                .AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            Provider = services.BuildServiceProvider();
            var roles = Provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { AppRoles.Assistant, AppRoles.Owner, AppRoles.Customer })
                roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }
        public AssistantInvitationsController Invitations() => new(Db, Users, Email, NullLogger<AssistantInvitationsController>.Instance);
        public async Task<AppUser> Assistant(string id = "sitter")
        {
            var user = new AppUser { Id = id, UserName = id + "@example.test", Email = id + "@example.test", FullName = id, AssistantStatus = AssistantAccountStatus.Active };
            Assert.True((await Users.CreateAsync(user, "password123")).Succeeded);
            Assert.True((await Users.AddToRoleAsync(user, AppRoles.Assistant)).Succeeded);
            return user;
        }
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
    private sealed class FakeEmail : IAssistantInvitationEmailSender
    {
        public string Token { get; private set; } = "";
        public bool Fail { get; set; }
        public Task SendAsync(string email, string token, CancellationToken ct)
        { if (Fail) throw new InvalidOperationException("SMTP unavailable"); Token = token; return Task.CompletedTask; }
    }
    private static ClaimsPrincipal Principal(string id, string role) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "test"));
    private static AssistantsController Controller(AppDbContext db, string id, string role = AppRoles.Assistant) => new(db)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(id, role) } } };

    [Fact]
    public async Task MultipleIntervalsAndRangeBlocks_RoundTripAndOverrideEveryBookingCheck()
    {
        await using var f = new Fixture(); var user = await f.Assistant();
        var api = Controller(f.Db, user.Id);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var days = Enum.GetValues<DayOfWeek>().SelectMany(day => new[] {
            new AssistantsController.WeeklyDay(day, true, new(8,0), new(12,0)),
            new AssistantsController.WeeklyDay(day, true, new(14,0), new(17,0)),
            new AssistantsController.WeeklyDay(day, true, new(19,0), new(21,0)) }).ToList();
        Assert.IsType<NoContentResult>(await api.Weekly("me", days, default));
        Assert.Equal(21, await f.Db.Availability.CountAsync());
        var invalid = days.Append(new(date.DayOfWeek, true, new(11,0), new(15,0))).ToList();
        Assert.IsType<BadRequestObjectResult>(await api.Weekly("me", invalid, default));
        Assert.Equal(21, await f.Db.Availability.CountAsync());
        f.Db.Services.Add(new ServiceOffering { Id = 10, Name = "Visit", Description = "Care", DurationMinutes = 60, IsActive = true });
        await f.Db.SaveChangesAsync();
        var availability = new AvailabilityService(f.Db);
        Assert.True(await availability.IsAvailableAsync(date, new(19,0), new(20,0), null, default, true, user.Id));
        Assert.False(await availability.IsAvailableAsync(date, new(11,30), new(12,30), null, default, true, user.Id));
        Assert.IsType<BadRequestObjectResult>(await api.AddBlock("me", new(date, null, null, date.AddDays(-1)), default));
        Assert.IsType<BadRequestObjectResult>(await api.AddBlock("me", new(date, new(13,0), null), default));
        var result = Assert.IsType<OkObjectResult>(await api.AddBlock("me", new(date, null, null, date.AddDays(2)), default));
        var block = Assert.IsType<AvailabilityDto>(result.Value);
        Assert.Equal(date.AddDays(2), block.EndDate);
        for (var offset = 0; offset < 3; offset++)
        {
            Assert.Empty(await availability.GetSlotsAsync(date.AddDays(offset), date.AddDays(offset), 10, default, user.Id));
            Assert.False(await availability.IsAvailableAsync(date.AddDays(offset), new(8,0), new(9,0), null, default, true, user.Id));
        }
        Assert.NotEmpty(await availability.GetSlotsAsync(date.AddDays(3), date.AddDays(3), 10, default, user.Id));
        Assert.IsType<OkObjectResult>(await api.EditBlock("me", block.Id, new(date, new(13,0), new(16,0), date.AddDays(2)), default));
        var slots = await availability.GetSlotsAsync(date, date, 10, default, user.Id);
        Assert.Contains(slots, slot => slot.StartTime == new TimeOnly(8,0));
        Assert.Contains(slots, slot => slot.StartTime == new TimeOnly(16,0));
        Assert.DoesNotContain(slots, slot => slot.StartTime == new TimeOnly(14,0));
        Assert.False(await availability.IsAvailableAsync(date.AddDays(1), new(15,30), new(16,30), null, default, true, user.Id));
        Assert.IsType<OkObjectResult>(await api.AddBlock("me", new(date.AddDays(4), null, null), default));
        Assert.IsType<NoContentResult>(await api.DeleteBlock("me", block.Id, default));
        Assert.True(await availability.IsAvailableAsync(date, new(14,0), new(15,0), null, default, true, user.Id));
        Assert.Empty(await availability.GetSlotsAsync(date.AddDays(4), date.AddDays(4), 10, default, user.Id));
    }

    [Fact]
    public async Task RangeBlock_RejectsNewBookingsAndConfirmationOfPendingBookings()
    {
        await using var f = new Fixture(); var user = await f.Assistant();
        var api = Controller(f.Db, user.Id);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        f.Db.Users.Add(new AppUser { Id = "client", FullName = "Client", ServiceArea = "Area", ServiceAddress = "Address" });
        f.Db.Dogs.Add(new Dog { Id = 1, UserId = "client", Name = "Dog" });
        f.Db.Services.Add(new ServiceOffering { Id = 10, Name = "Visit", Description = "Care", DurationMinutes = 60, IsActive = true });
        f.Db.Availability.Add(new AvailabilityRule { AssistantId = user.Id, DayOfWeek = date.DayOfWeek, StartTime = new(8,0), EndTime = new(17,0) });
        await f.Db.SaveChangesAsync();
        var service = new BookingService(f.Db, new AvailabilityService(f.Db));
        var request = new CreateBookingDto(1, 10, date, new(8,0), null, AssistantId: user.Id);
        var created = await service.CreateAsync("client", request, default);
        Assert.NotNull(created.Booking);
        Assert.IsType<OkObjectResult>(await api.AddBlock("me", new(date.AddDays(-1), null, null, date.AddDays(1)), default));
        Assert.Null((await service.CreateAsync("client", request with { StartTime = new(12,0) }, default)).Booking);
        Assert.Null((await service.ChangeStatusAsync(created.Booking.Id, BookingStatus.Confirmed, default)).Booking);
        Assert.Single(await f.Db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task AssistantCalendar_OnlyReturnsOwnBookings_AndRejectsFrozenAccounts()
    {
        await using var f = new Fixture(); var user = await f.Assistant(); var other = await f.Assistant("other");
        f.Db.Users.Add(new AppUser { Id = "client", FullName = "Client" });
        f.Db.Dogs.Add(new Dog { Id = 1, UserId = "client", Name = "Dog" });
        f.Db.Services.Add(new ServiceOffering { Id = 10, Name = "Visit", Description = "Care" });
        foreach (var sitter in new string?[] { user.Id, other.Id, null })
            f.Db.Bookings.Add(new Booking { UserId = "client", DogId = 1, ServiceOfferingId = 10, AssistantId = sitter });
        await f.Db.SaveChangesAsync();
        var api = Controller(f.Db, user.Id);
        var result = Assert.IsType<OkObjectResult>(await api.Bookings(default));
        var bookings = Assert.IsAssignableFrom<IEnumerable<BookingDto>>(result.Value);
        Assert.Equal(user.Id, Assert.Single(bookings).AssistantId);
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await api.Bookings(default));
    }

    [Fact]
    public async Task Invitation_IsEmailBound_Hashed_Expiring_AndSingleUse()
    {
        await using var f = new Fixture(); var api = f.Invitations();
        Assert.IsType<OkObjectResult>(await api.Invite(new("Invited@example.test", "  Alex Sitter  "), default));
        var stored = await f.Db.AssistantInvitations.SingleAsync();
        Assert.Equal("Alex Sitter", stored.FullName);
        Assert.NotEqual(f.Email.Token, stored.TokenHash);
        Assert.Equal(64, f.Email.Token.Length);
        Assert.InRange(stored.ExpiresAt - stored.CreatedAt, TimeSpan.FromHours(47), TimeSpan.FromHours(49));
        var accept = new AssistantInvitationsController.AcceptRequest(f.Email.Token, "wrong@example.test", "Sitter", "password123");
        Assert.IsType<BadRequestObjectResult>(await api.Accept(accept, default));
        Assert.Null(await f.Users.FindByEmailAsync("wrong@example.test"));
        accept = accept with { Email = "invited@example.test" };
        Assert.IsType<BadRequestObjectResult>(await api.Accept(accept with { Token = "invalid" }, default));
        Assert.IsType<OkObjectResult>(await api.Accept(accept, default));
        var user = await f.Users.FindByEmailAsync(accept.Email);
        Assert.NotNull(user); Assert.Equal(AssistantAccountStatus.Active, user.AssistantStatus);
        Assert.True(await f.Users.IsInRoleAsync(user, AppRoles.Assistant));
        Assert.False(await f.Users.IsInRoleAsync(user, AppRoles.Customer));
        Assert.True(await f.Users.CheckPasswordAsync(user, "password123"));
        Assert.NotNull(stored.UsedAt);
        Assert.IsType<BadRequestObjectResult>(await api.Accept(accept, default));
    }

    [Fact]
    public async Task ExpiredInvitationAndEmailFailure_DoNotCreateAccounts()
    {
        await using var f = new Fixture(); var api = f.Invitations();
        await api.Invite(new("expired@example.test", "Expired Sitter"), default);
        (await f.Db.AssistantInvitations.SingleAsync()).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await f.Db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await api.Accept(new(f.Email.Token, "expired@example.test", "Sitter", "password123"), default));
        Assert.Empty(await f.Db.Users.ToListAsync());
        f.Email.Fail = true;
        Assert.Equal(503, Assert.IsType<ObjectResult>(await api.Invite(new("failed@example.test", "Failed Sitter"), default)).StatusCode);
        Assert.False(await f.Db.AssistantInvitations.AnyAsync(i => i.Email == "failed@example.test"));
    }

    [Fact]
    public async Task FrozenAccount_RejectsPreviouslyIssuedPrincipal_AndReactivationRestoresAccess()
    {
        await using var f = new Fixture(); var user = await f.Assistant();
        var principal = Principal(user.Id, AppRoles.Assistant);
        var events = new AccountTokenEvents(f.Users);
        TokenValidatedContext Context() => new(new DefaultHttpContext(),
            new AuthenticationScheme("Bearer", "Bearer", typeof(JwtBearerHandler)), new JwtBearerOptions()) { Principal = principal };
        var active = Context(); await events.TokenValidated(active); Assert.Null(active.Result);
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        var frozen = Context(); await events.TokenValidated(frozen); Assert.NotNull(frozen.Result?.Failure);
        user.AssistantStatus = AssistantAccountStatus.Active; await f.Db.SaveChangesAsync();
        var restored = Context(); await events.TokenValidated(restored); Assert.Null(restored.Result);
    }

    [Fact]
    public async Task AvailabilityScope_RejectsOtherAssistants_AndFrozenWrites_PreservesData()
    {
        await using var f = new Fixture(); var user = await f.Assistant(); var other = await f.Assistant("other");
        var api = Controller(f.Db, user.Id); var date = DateOnly.FromDateTime(DateTime.Today.AddDays(4));
        Assert.IsType<ForbidResult>(await api.AddBlock(other.Id, new(date, null, null), default));
        Assert.IsType<OkObjectResult>(await api.AddBlock("me", new(date, null, null), default));
        var block = await f.Db.Availability.SingleAsync(); Assert.Equal(user.Id, block.AssistantId);
        var otherApi = Controller(f.Db, other.Id);
        Assert.IsType<NotFoundResult>(await otherApi.DeleteBlock("me", block.Id, default));
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await api.AddBlock("me", new(date, null, null), default));
        Assert.IsType<ForbidResult>(await api.RemovePhoto(default));
        Assert.IsType<ForbidResult>(await api.Upload(null, default));
        var owner = Controller(f.Db, "owner", AppRoles.Owner);
        Assert.IsType<OkObjectResult>(await owner.Availability(user.Id, default));
        Assert.IsType<NoContentResult>(await owner.Status(user.Id, new(AssistantAccountStatus.Active), default));
        Assert.Single(await f.Db.Availability.ToListAsync());
        Assert.IsType<NoContentResult>(await api.DeleteBlock("me", block.Id, default));
    }

    [Fact]
    public async Task Slots_CheckWeeklyHoursBlocksDurationBookingsAndStatus_WithoutAffectingOwner()
    {
        await using var f = new Fixture(); var user = await f.Assistant(); var date = DateOnly.FromDateTime(DateTime.Today.AddDays(4));
        f.Db.Services.Add(new ServiceOffering { Id = 10, Description = "Care", Name = "Hour", DurationMinutes = 60, IsActive = true });
        f.Db.Availability.AddRange(new AvailabilityRule { AssistantId = user.Id, DayOfWeek = date.DayOfWeek, StartTime = new(8,0), EndTime = new(17,0) },
            new AvailabilityRule { AssistantId = user.Id, SpecificDate = date, StartTime = new(13,0), EndTime = new(15,0), IsAvailable = false, Notes = "private reason" });
        f.Db.Bookings.Add(new Booking { UserId = "client", AssistantId = user.Id, Date = date, StartTime = new(10,0), EndTime = new(11,0), Status = BookingStatus.Confirmed });
        await f.Db.SaveChangesAsync(); var service = new AvailabilityService(f.Db);
        var slots = await service.GetSlotsAsync(date, date, 10, default, user.Id);
        Assert.Contains(slots, s => s.StartTime == new TimeOnly(8,0));
        Assert.DoesNotContain(slots, s => s.StartTime == new TimeOnly(9,30));
        Assert.DoesNotContain(slots, s => s.StartTime == new TimeOnly(12,30));
        Assert.DoesNotContain(slots, s => s.StartTime == new TimeOnly(16,30));
        Assert.Empty(await service.GetSlotsAsync(date.AddDays(1), date.AddDays(1), 10, default, user.Id));
        Assert.True(await service.IsAvailableAsync(date, new(10,0), new(11,0), null, default));
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync(date, date, 10, default, user.Id));
        Assert.False(await service.IsAvailableAsync(date, new(8,0), new(9,0), null, default, true, user.Id));
        Assert.All(await service.GetDayScheduleAsync(date, 10, default, user.Id), s => { Assert.False(s.IsBookable); Assert.Equal("Unavailable", s.Status); });
        user.AssistantStatus = AssistantAccountStatus.Active; await f.Db.SaveChangesAsync();
        Assert.Equal(slots, await service.GetSlotsAsync(date, date, 10, default, user.Id));
        Assert.Single(await f.Db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task BookingSubmissionAndConfirmation_RecheckFrozenSitter()
    {
        await using var f = new Fixture(); var sitter = await f.Assistant();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(4));
        var customer = new AppUser { Id = "client", UserName = "client", FullName = "Client", ServiceArea = "Area", ServiceAddress = "Address" };
        f.Db.Users.Add(customer); f.Db.Dogs.Add(new Dog { Id = 1, UserId = customer.Id, Name = "Dog" });
        f.Db.Services.Add(new ServiceOffering { Id = 10, Description = "Care", Name = "Walk", DurationMinutes = 60, IsActive = true });
        f.Db.Availability.Add(new AvailabilityRule { AssistantId = sitter.Id, DayOfWeek = date.DayOfWeek, StartTime = new(8,0), EndTime = new(17,0) });
        await f.Db.SaveChangesAsync();
        var bookings = new BookingService(f.Db, new AvailabilityService(f.Db));
        var request = new CreateBookingDto(1, 10, date, new(8,0), null, AssistantId: sitter.Id);
        var created = await bookings.CreateAsync(customer.Id, request, default); Assert.NotNull(created.Booking); Assert.Equal(sitter.Id, created.Booking.AssistantId);
        sitter.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.Null((await bookings.CreateAsync(customer.Id, request with { StartTime = new(12,0) }, default)).Booking);
        Assert.Null((await bookings.ChangeStatusAsync(created.Booking.Id, BookingStatus.Confirmed, default)).Booking);
        Assert.Single(await f.Db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Overnight_ChecksEveryCareWindow_AndFrozenStatus()
    {
        await using var f = new Fixture(); var user = await f.Assistant();
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(4));
        f.Db.Services.Add(new ServiceOffering { Id = 10, Name = "Overnight", Description = "Stay", DurationMinutes = 660, IsActive = true, IsOvernightStay = true });
        foreach (var day in Enum.GetValues<DayOfWeek>())
            f.Db.Availability.Add(new AvailabilityRule { AssistantId = user.Id, DayOfWeek = day, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue });
        await f.Db.SaveChangesAsync(); var service = new AvailabilityService(f.Db);
        Assert.True((await service.CheckOvernightAsync(date, date.AddDays(2), 10, default, user.Id))!.IsAvailable);
        f.Db.Availability.Add(new AvailabilityRule { AssistantId = user.Id, SpecificDate = date.AddDays(1), EndDate = date.AddDays(2), StartTime = new(14,0), EndTime = new(15,0), IsAvailable = false });
        await f.Db.SaveChangesAsync();
        Assert.False((await service.CheckOvernightAsync(date, date.AddDays(2), 10, default, user.Id))!.IsAvailable);
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.False((await service.CheckOvernightAsync(date, date.AddDays(1), 10, default, user.Id))!.IsAvailable);
    }

    [Fact]
    public async Task PublicListingAndPhotos_ExcludeFrozen_AndRetainPhotoForReactivation()
    {
        await using var f = new Fixture(); var user = await f.Assistant(); var api = Controller(f.Db, user.Id);
        byte[] bytes = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        using var stream = new MemoryStream(bytes);
        Assert.IsType<NoContentResult>(await api.Upload(new FormFile(stream, 0, bytes.Length, "photo", "photo.png"), default));
        Assert.IsType<FileContentResult>(await api.Photo(user.Id, default));
        user.AssistantStatus = AssistantAccountStatus.Frozen; await f.Db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await api.Photo(user.Id, default));
        var listing = Assert.IsType<OkObjectResult>(await api.Public(default));
        Assert.Equal("[]", System.Text.Json.JsonSerializer.Serialize(listing.Value));
        Assert.Equal(bytes, user.ProfilePhotoData);
        user.AssistantStatus = AssistantAccountStatus.Active; await f.Db.SaveChangesAsync();
        Assert.Equal(bytes, Assert.IsType<FileContentResult>(await api.Photo(user.Id, default)).FileContents);
        Assert.IsType<NoContentResult>(await api.RemovePhoto(default));
        Assert.Empty(user.ProfilePhotoData);
    }

    [Fact]
    public async Task HistoricalBookingPhoto_IsPrivateAndSurvivesFreezing()
    {
        await using var f = new Fixture(); var user = await f.Assistant();
        user.ProfilePhotoData = [1,2,3]; user.ProfilePhotoContentType = "image/png";
        user.AssistantStatus = AssistantAccountStatus.Frozen;
        f.Db.Bookings.Add(new Booking { Id = 12, UserId = "client", AssistantId = user.Id });
        await f.Db.SaveChangesAsync();
        BookingsController Api(string id) => new(f.Db, null!, null!, null!, NullLogger<BookingsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(id, AppRoles.Customer) } } };
        Assert.IsType<FileContentResult>(await Api("client").AssistantPhoto(12, default));
        Assert.IsType<NotFoundResult>(await Api("another-client").AssistantPhoto(12, default));
    }

    [Theory]
    [InlineData(typeof(BookingsController))]
    [InlineData(typeof(DogsController))]
    public void CustomerControllers_DoNotAuthorizeAssistantRole(Type controller)
    {
        var attribute = Assert.Single(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.DoesNotContain(AppRoles.Assistant, attribute.Roles!.Split(','));
    }
}

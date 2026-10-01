using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<ServiceOffering> Services => Set<ServiceOffering>();
    public DbSet<AvailabilityRule> Availability => Set<AvailabilityRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<SiteContent> SiteContent => Set<SiteContent>();
    public DbSet<GalleryPhoto> GalleryPhotos => Set<GalleryPhoto>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();

    public DbSet<AssistantInvitation> AssistantInvitations => Set<AssistantInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AppUser>().Property(u => u.AssistantStatus).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AssistantInvitation>().HasIndex(i => i.TokenHash).IsUnique();
        modelBuilder.Entity<AssistantInvitation>().Property(i => i.UsedAt).IsConcurrencyToken();
        modelBuilder.Entity<AvailabilityRule>().HasOne(r => r.Assistant).WithMany()
            .HasForeignKey(r => r.AssistantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>().HasOne(b => b.Assistant).WithMany()
            .HasForeignKey(b => b.AssistantId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.Dogs).WithOne(dog => dog.User)
            .HasForeignKey(dog => dog.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.Bookings).WithOne(booking => booking.User)
            .HasForeignKey(booking => booking.UserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.PasswordResetCodes).WithOne(code => code.User)
            .HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PasswordResetCode>()
            .HasIndex(code => new { code.UserId, code.CreatedAt });

        modelBuilder.Entity<Dog>()
            .HasMany(dog => dog.Bookings).WithOne(booking => booking.Dog)
            .HasForeignKey(booking => booking.DogId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceOffering>()
            .HasMany(service => service.Bookings).WithOne(booking => booking.ServiceOffering)
            .HasForeignKey(booking => booking.ServiceOfferingId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>().Property(booking => booking.Status)
            .HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AppUser>().Property(user => user.ApprovalStatus)
            .HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Booking>()
            .HasIndex(booking => new { booking.Date, booking.StartTime, booking.EndTime });

        modelBuilder.Entity<AvailabilityRule>()
            .HasIndex(rule => new { rule.SpecificDate, rule.DayOfWeek, rule.StartTime, rule.EndTime });
        modelBuilder.Entity<AvailabilityRule>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Availability_OneScope",
                "(\"SpecificDate\" IS NOT NULL) <> (\"DayOfWeek\" IS NOT NULL)"));
        modelBuilder.Entity<AvailabilityRule>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_Availability_TimeRange",
                "\"EndTime\" > \"StartTime\""));

        modelBuilder.Entity<ServiceOffering>().HasData(
            new ServiceOffering { Id = 1, Name = "30-minute dog walk", Description = "A focused neighborhood walk with time to sniff, move, and reset.", DurationMinutes = 30, Price = 24m, IsActive = true },
            new ServiceOffering { Id = 2, Name = "60-minute dog walk", Description = "A longer, enriching outing for active dogs who need extra exercise.", DurationMinutes = 60, Price = 38m, IsActive = true },
            new ServiceOffering { Id = 3, Name = "Drop-in visit", Description = "Food, fresh water, playtime, medication support, and a reassuring check-in.", DurationMinutes = 30, Price = 22m, IsActive = true },
            new ServiceOffering { Id = 4, Name = "Overnight stay", Description = "Overnight companionship with morning care, a midday visit, and evening care. Times can be customized for each stay.", DurationMinutes = 660, Price = 95m, IsActive = true, IsOvernightStay = true });
    }
}

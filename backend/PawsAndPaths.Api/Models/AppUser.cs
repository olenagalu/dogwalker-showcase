using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PawsAndPaths.Api.Models;

public class AppUser : IdentityUser
{
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(160)] public string ServiceArea { get; set; } = string.Empty;
    [MaxLength(300)] public string ServiceAddress { get; set; } = string.Empty;
    public AccountApprovalStatus ApprovalStatus { get; set; } = AccountApprovalStatus.Approved;
    [MaxLength(100)] public string ProfilePhotoContentType { get; set; } = string.Empty;
    public byte[] ProfilePhotoData { get; set; } = [];
    public AssistantAccountStatus? AssistantStatus { get; set; }
    public ICollection<Dog> Dogs { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<PasswordResetCode> PasswordResetCodes { get; set; } = [];
}

public enum AccountApprovalStatus
{
    Pending,
    Approved,
    Declined
}

public enum AssistantAccountStatus { Active, Frozen }

public static class AppRoles
{
    public const string Assistant = "Assistant";
    public const string Customer = "Customer";
    public const string Owner = "Owner";
}

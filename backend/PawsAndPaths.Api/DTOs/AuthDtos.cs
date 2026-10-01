using System.ComponentModel.DataAnnotations;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.DTOs;

public record RegisterDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, MaxLength(160)] string ServiceArea,
    [Required, MaxLength(300)] string ServiceAddress,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record GoogleSignInDto([Required] string Credential);

public record AuthResponseDto(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);

public record ForgotPasswordDto([Required, EmailAddress] string Email);

public record ForgotPasswordResponseDto(string Message, string? ResetCode = null);

public record ResetPasswordDto(
    [Required, EmailAddress] string Email,
    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the 6-digit verification code.")] string Code,
    [Required, MinLength(8), MaxLength(100)] string NewPassword);

public record UpdateProfileDto(
    [Required, MaxLength(120)] string FullName,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, MaxLength(160)] string ServiceArea,
    [Required, MaxLength(300)] string ServiceAddress);

public record UserProfileDto(string Id, string FullName, string Email, string Phone, string Role,
    string ServiceArea, string ServiceAddress, AccountApprovalStatus ApprovalStatus, bool HasProfilePhoto);

public record CustomerSummaryDto(string Id, string FullName, string Email, string Phone,
    string ServiceArea, string ServiceAddress, AccountApprovalStatus ApprovalStatus,
    bool HasProfilePhoto, IReadOnlyList<DogDto> Dogs);

public record CreateOwnerCustomerWithDogDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, MaxLength(160)] string ServiceArea,
    [Required, MaxLength(300)] string ServiceAddress,
    [Required, MaxLength(80)] string DogName,
    [MaxLength(80)] string? DogBreed,
    [Range(0, 30)] int? DogAge);

public record UpdateApprovalDto(AccountApprovalStatus Status);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(100)] string NewPassword);

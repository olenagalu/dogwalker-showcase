using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record ContactMessageDto(
    [Required, MaxLength(120)] string Name,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(10), MaxLength(3000)] string Message);

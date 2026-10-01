using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record ServiceWriteDto(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(1000)] string Description,
    [Range(5, 1440)] int DurationMinutes,
    [Range(typeof(decimal), "0.01", "10000")] decimal Price,
    bool IsActive,
    bool IsOvernightStay = false);

public record ServiceDto(int Id, string Name, string Description, int DurationMinutes, decimal Price, bool IsActive, bool IsOvernightStay);

using System.ComponentModel.DataAnnotations;

namespace PawsAndPaths.Api.DTOs;

public record DogWriteDto(
    [Required, MaxLength(80)] string Name,
    [MaxLength(80)] string? Breed,
    [Range(0, 30)] int? Age,
    [MaxLength(2000)] string? CareInstructions,
    [MaxLength(1500)] string? BehavioralNotes,
    [MaxLength(1500)] string? MedicalNotes);

public record CreateDogDto(
    [Required, MaxLength(80)] string Name,
    [MaxLength(80)] string? Breed,
    [Range(0, 30)] int? Age,
    [MaxLength(2000)] string? CareInstructions,
    [MaxLength(1500)] string? BehavioralNotes,
    [MaxLength(1500)] string? MedicalNotes,
    [Required, MaxLength(100)] string Password);

public record DogDto(
    int Id, string Name, string Breed, int? Age,
    string CareInstructions, string BehavioralNotes, string MedicalNotes, bool HasPhoto);

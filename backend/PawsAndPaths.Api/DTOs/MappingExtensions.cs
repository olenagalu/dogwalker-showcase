using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.DTOs;

public static class MappingExtensions
{
    public static DogDto ToDto(this Dog dog) => new(
        dog.Id, dog.Name, dog.Breed, dog.Age,
        dog.CareInstructions, dog.BehavioralNotes, dog.MedicalNotes, dog.PhotoData.Length > 0);

    public static ServiceDto ToDto(this ServiceOffering service) => new(
        service.Id, service.Name, service.Description,
        service.DurationMinutes, service.Price, service.IsActive, service.IsOvernightStay);

    public static AvailabilityDto ToDto(this AvailabilityRule rule) => new(
        rule.Id, rule.DayOfWeek, rule.SpecificDate, rule.StartTime,
        rule.EndTime, rule.IsAvailable, rule.Notes, rule.EndDate);

    public static BookingDto ToDto(this Booking booking) => new(
        booking.Id, booking.User.FullName, booking.User.Email ?? string.Empty,
        booking.User.PhoneNumber ?? string.Empty, booking.User.ServiceAddress,
        booking.DogId, booking.Dog.Name, booking.Dog.Breed, booking.ServiceOfferingId,
        booking.ServiceOffering.Name, booking.Date, booking.StartTime,
        booking.EndTime, booking.Price, booking.SpecialInstructions,
        booking.Status, booking.CreatedAt, booking.EndDate, booking.IsOvernightStay,
        booking.OvernightStartTime, booking.OvernightEndTime,
        booking.MiddayStartTime, booking.MiddayEndTime,
        booking.UserId, booking.User.ApprovalStatus, booking.AssistantId, booking.Assistant?.FullName);
}

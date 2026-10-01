using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public readonly record struct BookingWindow(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Label);

public static class BookingSchedule
{
    public static IReadOnlyList<BookingWindow> Windows(Booking booking)
    {
        if (!booking.IsOvernightStay || booking.EndDate is null)
            return [new BookingWindow(booking.Date, booking.StartTime, booking.EndTime, booking.ServiceOffering?.Name ?? "Booking")];

        var overnightStart = booking.OvernightStartTime ?? new TimeOnly(22, 0);
        var overnightEnd = booking.OvernightEndTime ?? new TimeOnly(9, 0);
        var middayStart = booking.MiddayStartTime ?? new TimeOnly(14, 0);
        var middayEnd = booking.MiddayEndTime ?? new TimeOnly(15, 0);
        var windows = new List<BookingWindow>();
        for (var date = booking.Date; date <= booking.EndDate.Value; date = date.AddDays(1))
        {
            if (date > booking.Date)
                windows.Add(new BookingWindow(date, TimeOnly.MinValue, overnightEnd, "Overnight · until"));
            if (date > booking.Date && date < booking.EndDate.Value)
                windows.Add(new BookingWindow(date, middayStart, middayEnd, "Midday visit"));
            if (date < booking.EndDate.Value)
                windows.Add(new BookingWindow(date, overnightStart, new TimeOnly(23, 59), "Overnight · from"));
        }
        return windows;
    }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PawsAndPaths.Api.Data;

namespace PawsAndPaths.Api.Services;

public record WorkingHours(TimeOnly Start, TimeOnly End, TimeOnly EmergencyStart,
    TimeOnly EmergencyEnd, [Range(0, 10000)] decimal EmergencySurcharge, bool EmergencyEnabled)
{
    public const string Key = "working-hours";
    public static WorkingHours Default => new(new(7, 0), new(23, 0), new(23, 0), new(6, 0), 0, false);
    public static async Task<WorkingHours> ReadAsync(AppDbContext db, CancellationToken ct)
    {
        var json = await db.SiteContent.Where(x => x.Key == Key).Select(x => x.Text).SingleOrDefaultAsync(ct);
        return json is null ? Default : JsonSerializer.Deserialize<WorkingHours>(json)!;
    }
    public bool IsEmergency(TimeOnly time) => EmergencyEnabled && (EmergencyStart < EmergencyEnd
        ? time >= EmergencyStart && time < EmergencyEnd
        : time >= EmergencyStart || time < EmergencyEnd);
}

using System.Text.Json.Serialization;
using Jadwal.Domain.Enums;

namespace Jadwal.Domain.Models;

public record TimetableSnapshot
{
    [JsonPropertyName("academicYear")]
    public string AcademicYear { get; init; } = string.Empty;

    [JsonPropertyName("weekNumber")]
    public int? WeekNumber { get; init; }

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("periods")]
    public IReadOnlyList<PeriodOccurrence> Periods { get; init; } = Array.Empty<PeriodOccurrence>();

    public TimetableSnapshot() { }

    public TimetableSnapshot(string academicYear, int? weekNumber, IEnumerable<PeriodOccurrence> periods, DateTime? generatedAt = null)
    {
        AcademicYear = academicYear ?? string.Empty;
        WeekNumber = weekNumber;
        Periods = periods?.ToList().AsReadOnly() ?? (IReadOnlyList<PeriodOccurrence>)Array.Empty<PeriodOccurrence>();
        GeneratedAt = generatedAt ?? DateTime.UtcNow;
    }

    public IReadOnlyList<PeriodOccurrence> GetPeriodsForDay(JadwalDayOfWeek day)
    {
        return Periods.Where(p => p.Day == day).ToList().AsReadOnly();
    }
}

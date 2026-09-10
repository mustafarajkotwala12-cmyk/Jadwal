using System.Text.Json.Serialization;
using Jadwal.Domain.Enums;

namespace Jadwal.Domain.Models;

public record PeriodOccurrence
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("day")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JadwalDayOfWeek Day { get; init; }

    [JsonPropertyName("date")]
    public string DateString { get; init; }

    [JsonPropertyName("period")]
    public string PeriodName { get; init; }

    [JsonPropertyName("startTime")]
    public string StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public string EndTime { get; init; }

    [JsonPropertyName("subject")]
    public string Subject { get; init; }

    [JsonPropertyName("details")]
    public string Details { get; init; }

    [JsonPropertyName("changeRecord")]
    public TimetableChangeRecord? ChangeRecord { get; init; }

    [JsonConstructor]
    public PeriodOccurrence(
        string? id,
        JadwalDayOfWeek day,
        string dateString,
        string periodName,
        string startTime,
        string endTime,
        string subject,
        string details,
        TimetableChangeRecord? changeRecord = null)
    {
        Day = day;
        DateString = dateString ?? string.Empty;
        PeriodName = periodName ?? string.Empty;
        StartTime = startTime ?? string.Empty;
        EndTime = endTime ?? string.Empty;
        Subject = subject ?? string.Empty;
        Details = details ?? string.Empty;
        ChangeRecord = changeRecord;
        Id = string.IsNullOrWhiteSpace(id)
            ? ComputeDeterministicId(dateString, periodName)
            : id;
    }

    [JsonIgnore]
    public string StartTime12H => FormatTo12Hour(StartTime);

    [JsonIgnore]
    public string EndTime12H => FormatTo12Hour(EndTime);

    public static string ComputeDeterministicId(string? dateString, string? periodName)
    {
        var safeDate = (dateString ?? string.Empty).Trim();
        var safePeriod = (periodName ?? string.Empty).Trim().Replace(" ", "_");
        return $"{safeDate}_{safePeriod}";
    }

    public static string FormatTo12Hour(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return string.Empty;
        var trimmed = timeStr.Trim();
        if (TimeOnly.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var t) ||
            TimeOnly.TryParse(trimmed, out t))
        {
            return t.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var dt) ||
            DateTime.TryParse(trimmed, out dt))
        {
            return dt.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        }
        return trimmed;
    }
}

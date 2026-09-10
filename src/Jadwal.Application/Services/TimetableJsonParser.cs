using System.Text.Json;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public class RawTimetablePayload
{
    public string? academicYear { get; set; }
    public int? weekNumber { get; set; }
    public string? startDate { get; set; }
    public string? endDate { get; set; }
    public List<RawPeriodEntry>? entries { get; set; }
    public List<RawPeriodEntry>? periods { get; set; }
}

public class RawPeriodEntry
{
    public string? day { get; set; }
    public string? date { get; set; }
    public string? period { get; set; }
    public string? startTime { get; set; }
    public string? endTime { get; set; }
    public string? subject { get; set; }
    public string? details { get; set; }
}

public class TimetableJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TimetableSnapshot ParseJson(string json)
    {
        var payload = JsonSerializer.Deserialize<RawTimetablePayload>(json, JsonOptions);
        if (payload == null)
        {
            throw new FormatException("Failed to deserialize timetable JSON payload.");
        }

        var rawList = payload.entries ?? payload.periods ?? new List<RawPeriodEntry>();
        var normalizedPeriods = new List<PeriodOccurrence>();

        foreach (var raw in rawList)
        {
            var day = JadwalDayOfWeekExtensions.ParseFromDayString(raw.day ?? string.Empty);
            var dateStr = (raw.date ?? string.Empty).Trim();
            var periodName = (raw.period ?? string.Empty).Trim();
            var startTime = (raw.startTime ?? string.Empty).Trim();
            var endTime = (raw.endTime ?? string.Empty).Trim();
            var subject = (raw.subject ?? string.Empty).Trim();
            var details = (raw.details ?? string.Empty).Trim();

            normalizedPeriods.Add(new PeriodOccurrence(
                id: null,
                day: day,
                dateString: dateStr,
                periodName: periodName,
                startTime: startTime,
                endTime: endTime,
                subject: subject,
                details: details
            ));
        }

        return new TimetableSnapshot(
            academicYear: payload.academicYear ?? string.Empty,
            weekNumber: payload.weekNumber,
            periods: normalizedPeriods
        );
    }
}

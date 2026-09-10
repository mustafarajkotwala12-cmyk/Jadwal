using System.Text.Json;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Integrations.Jamia;

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

public class TimetableImporter
{
    public TimetableSnapshot ImportFromJson(string json)
    {
        return TimetableJsonParser.ParseJson(json);
    }

    public async Task<TimetableSnapshot> ImportFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return await Task.Run(() => ExcelTimetableParser.Parse(filePath), ct);
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        return ImportFromJson(json);
    }
}

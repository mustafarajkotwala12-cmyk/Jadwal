using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public static class ExcelTimetableParser
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static TimetableSnapshot Parse(string excelFilePath)
    {
        using var stream = File.OpenRead(excelFilePath);
        return Parse(stream);
    }

    public static TimetableSnapshot Parse(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        // 1. Load Shared Strings
        var sharedStrings = new List<string>();
        var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (ssEntry != null)
        {
            using var ssStream = ssEntry.Open();
            var ssDoc = XDocument.Load(ssStream);
            foreach (var si in ssDoc.Descendants(Ns + "si"))
            {
                var text = string.Concat(si.Descendants(Ns + "t").Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        // 2. Load Sheet1
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml"));
        if (sheetEntry == null)
        {
            throw new FormatException("Could not find worksheet in Excel package.");
        }

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);

        // Map cells: (row, col) -> text value
        var cells = new Dictionary<(int Row, int Col), string>();

        foreach (var rowEl in sheetDoc.Descendants(Ns + "row"))
        {
            var rAttr = (string?)rowEl.Attribute("r");
            if (!int.TryParse(rAttr, out var rowNum)) continue;

            foreach (var cEl in rowEl.Descendants(Ns + "c"))
            {
                var rCoord = (string?)cEl.Attribute("r");
                if (string.IsNullOrEmpty(rCoord)) continue;

                var colNum = ColumnNameToNumber(rCoord);
                var type = (string?)cEl.Attribute("t");
                var vEl = cEl.Element(Ns + "v");
                var rawVal = vEl?.Value;

                string val = string.Empty;
                if (type == "s" && int.TryParse(rawVal, out var sIndex) && sIndex >= 0 && sIndex < sharedStrings.Count)
                {
                    val = sharedStrings[sIndex];
                }
                else if (type == "inlineStr")
                {
                    val = string.Concat(cEl.Descendants(Ns + "t").Select(t => t.Value));
                }
                else if (rawVal != null)
                {
                    val = rawVal;
                }

                if (!string.IsNullOrWhiteSpace(val))
                {
                    cells[(rowNum, colNum)] = val.Trim();
                }
            }
        }

        // 3. Academic Year (A3)
        cells.TryGetValue((3, 1), out var academicYear);
        academicYear ??= "1447 / 1448";

        // 4. Week Number & Dates (Row 23)
        int? weekNumber = null;
        DateTime? startDate = null;

        if (cells.TryGetValue((23, 1), out var weekText))
        {
            var match = Regex.Match(weekText, @"Week\s*#?\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var wn))
            {
                weekNumber = wn;
            }
        }

        if (cells.TryGetValue((23, 2), out var dateRangeText))
        {
            var match = Regex.Match(dateRangeText, @"\[\s*(\d{2}\s+\w+\s+\d{4})\s*-\s*(\d{2}\s+\w+\s+\d{4})\s*\]");
            if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
            {
                startDate = sd;
            }
        }

        // 5. Monday - Friday Periods (Row 6 = Period name, Row 7 = Time range)
        var maxCol = cells.Keys.Count > 0 ? cells.Keys.Max(k => k.Col) : 12;
        var weekdayPeriods = new List<(int Col, string PeriodName, string StartTime, string EndTime)>();

        for (int c = 2; c <= maxCol; c++)
        {
            if (cells.TryGetValue((6, c), out var periodName) &&
                cells.TryGetValue((7, c), out var timeStr))
            {
                var times = ParseTimeRange(timeStr);
                if (times != null)
                {
                    weekdayPeriods.Add((c, periodName, times.Value.Start, times.Value.End));
                }
            }
        }

        var normalizedPeriods = new List<PeriodOccurrence>();

        var dayConfigs = new (int SubjectRow, JadwalDayOfWeek Day, int DayOffset)[]
        {
            (8, JadwalDayOfWeek.Monday, 0),
            (10, JadwalDayOfWeek.Tuesday, 1),
            (12, JadwalDayOfWeek.Wednesday, 2),
            (14, JadwalDayOfWeek.Thursday, 3),
            (16, JadwalDayOfWeek.Friday, 4)
        };

        foreach (var config in dayConfigs)
        {
            var dateStr = startDate?.AddDays(config.DayOffset).ToString("yyyy-MM-dd") ?? string.Empty;
            var detailRow = config.SubjectRow + 1;

            foreach (var p in weekdayPeriods)
            {
                if (cells.TryGetValue((config.SubjectRow, p.Col), out var subject) && !string.IsNullOrWhiteSpace(subject))
                {
                    cells.TryGetValue((detailRow, p.Col), out var details);
                    normalizedPeriods.Add(new PeriodOccurrence(
                        id: null,
                        day: config.Day,
                        dateString: dateStr,
                        periodName: p.PeriodName,
                        startTime: p.StartTime,
                        endTime: p.EndTime,
                        subject: subject,
                        details: details ?? string.Empty
                    ));
                }
            }
        }

        // 6. Saturday Periods (Row 18 = Time, Row 19 = Subject, Row 20 = Details)
        var saturdayDateStr = startDate?.AddDays(5).ToString("yyyy-MM-dd") ?? string.Empty;
        int satPeriodIdx = 1;

        for (int c = 2; c <= maxCol; c++)
        {
            if (cells.TryGetValue((18, c), out var satTimeStr))
            {
                var times = ParseTimeRange(satTimeStr);
                if (times != null && cells.TryGetValue((19, c), out var subject) && !string.IsNullOrWhiteSpace(subject))
                {
                    cells.TryGetValue((20, c), out var details);
                    normalizedPeriods.Add(new PeriodOccurrence(
                        id: null,
                        day: JadwalDayOfWeek.Saturday,
                        dateString: saturdayDateStr,
                        periodName: $"Period {satPeriodIdx}",
                        startTime: times.Value.Start,
                        endTime: times.Value.End,
                        subject: subject,
                        details: details ?? string.Empty
                    ));
                    satPeriodIdx++;
                }
            }
        }

        return new TimetableSnapshot(
            academicYear: academicYear,
            weekNumber: weekNumber,
            periods: normalizedPeriods
        );
    }

    private static (string Start, string End)? ParseTimeRange(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text.Trim(), @"^(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$");
        if (!match.Success) return null;
        return (match.Groups[1].Value, match.Groups[2].Value);
    }

    private static int ColumnNameToNumber(string cellReference)
    {
        int col = 0;
        foreach (char c in cellReference)
        {
            if (char.IsLetter(c))
            {
                col = (col * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            else break;
        }
        return col;
    }
}

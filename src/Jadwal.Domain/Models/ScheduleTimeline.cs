using System.Text.Json.Serialization;
using Jadwal.Domain.Enums;

namespace Jadwal.Domain.Models;

public record BreakBlock(
    string Id,
    string Name,
    string StartTime,
    string EndTime,
    int DurationMinutes
);

public enum ScheduleTimelineItemKind
{
    ClassPeriod,
    BreakBlock
}

public record ScheduleTimelineItem
{
    public ScheduleTimelineItemKind Kind { get; init; }
    public PeriodOccurrence? Period { get; init; }
    public ClassLiveStatus Status { get; init; }
    public BreakBlock? Break { get; init; }

    public static ScheduleTimelineItem ForPeriod(PeriodOccurrence period, ClassLiveStatus status) => new()
    {
        Kind = ScheduleTimelineItemKind.ClassPeriod,
        Period = period,
        Status = status
    };

    public static ScheduleTimelineItem ForBreak(BreakBlock breakBlock) => new()
    {
        Kind = ScheduleTimelineItemKind.BreakBlock,
        Break = breakBlock,
        Status = ClassLiveStatus.Upcoming
    };
}

public record DayScheduleRule(
    JadwalDayOfWeek Day,
    bool HasPhysicalTraining,
    bool IsHalfDay,
    string DaySubtitle,
    string Row1Title,
    string Row2Title
)
{
    public static DayScheduleRule RuleFor(JadwalDayOfWeek day) => day switch
    {
        JadwalDayOfWeek.Monday or JadwalDayOfWeek.Tuesday or JadwalDayOfWeek.Wednesday or JadwalDayOfWeek.Thursday => new(
            Day: day,
            HasPhysicalTraining: true,
            IsHalfDay: false,
            DaySubtitle: "Full Academic Schedule • 10 Periods",
            Row1Title: "Morning Prep & Early Sessions (Periods 1–5)",
            Row2Title: "Midday & Afternoon Sessions (Periods 6–10)"
        ),
        JadwalDayOfWeek.Friday => new(
            Day: day,
            HasPhysicalTraining: false,
            IsHalfDay: false,
            DaySubtitle: "Jumua Mubarak • No Morning PT (Periods 2–10)",
            Row1Title: "Morning Sessions (Periods 2–5)",
            Row2Title: "Midday & Post-Jumua Sessions (Periods 6–10)"
        ),
        JadwalDayOfWeek.Saturday => new(
            Day: day,
            HasPhysicalTraining: false,
            IsHalfDay: true,
            DaySubtitle: "Saturday Half-Day • Concludes at 1:15 PM",
            Row1Title: "Early Morning Sessions (Periods 1–4)",
            Row2Title: "Late Morning Sessions (Periods 5–8)"
        ),
        JadwalDayOfWeek.Sunday => new(
            Day: day,
            HasPhysicalTraining: false,
            IsHalfDay: true,
            DaySubtitle: "Weekend • No Classes Scheduled",
            Row1Title: "Weekend",
            Row2Title: string.Empty
        ),
        _ => RuleFor(JadwalDayOfWeek.Monday)
    };
}

public static class ScheduleTimelineBuilder
{
    private static int? ParseMinutes(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return null;
        var parts = timeStr.Trim().Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
        {
            return h * 60 + m;
        }
        return null;
    }

    public static string FormatDuration(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        if (h > 0 && m > 0) return $"{h}h {m}m";
        if (h > 0) return $"{h}h";
        return $"{m}m";
    }

    private static string IntelligentBreakName(int startMin, int endMin, int durationMin)
    {
        if (startMin < 8 * 60) return "Morning Preparation";
        if (startMin >= 10 * 60 && startMin < 12 * 60) return "Recess";
        if (startMin >= 12 * 60 && startMin < 14 * 60) return "Lunch & Namaz Break";
        if (startMin >= 14 * 60) return "Afternoon Break";
        return "Break";
    }

    public static IReadOnlyList<ScheduleTimelineItem> BuildTimeline(
        IEnumerable<PeriodOccurrence> periods,
        TimeOnly? currentTime = null)
    {
        var now = currentTime ?? TimeOnly.FromDateTime(DateTime.Now);
        var sorted = periods
            .OrderBy(p => ParseMinutes(p.StartTime) ?? 0)
            .ToList();

        var items = new List<ScheduleTimelineItem>();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var status = current.ChangeRecord != null
                ? ClassLiveStatus.Changed
                : ClassLiveStatus.Compute(current.StartTime, current.EndTime, now);

            items.Add(ScheduleTimelineItem.ForPeriod(current, status));

            if (i + 1 < sorted.Count)
            {
                var next = sorted[i + 1];
                var endMin = ParseMinutes(current.EndTime);
                var nextStartMin = ParseMinutes(next.StartTime);

                if (endMin.HasValue && nextStartMin.HasValue)
                {
                    var gap = nextStartMin.Value - endMin.Value;
                    if (gap >= 10)
                    {
                        var name = IntelligentBreakName(endMin.Value, nextStartMin.Value, gap);
                        var breakBlock = new BreakBlock(
                            Id: $"break_{current.EndTime}_{next.StartTime}",
                            Name: name,
                            StartTime: current.EndTime,
                            EndTime: next.StartTime,
                            DurationMinutes: gap
                        );
                        items.Add(ScheduleTimelineItem.ForBreak(breakBlock));
                    }
                }
            }
        }

        return items.AsReadOnly();
    }

    public static (IReadOnlyList<ScheduleTimelineItem> Row1, IReadOnlyList<ScheduleTimelineItem> Row2) SplitIntoTwoHorizontalRows(
        IReadOnlyList<ScheduleTimelineItem> items,
        JadwalDayOfWeek day)
    {
        if (items.Count == 0) return (Array.Empty<ScheduleTimelineItem>(), Array.Empty<ScheduleTimelineItem>());

        switch (day)
        {
            case JadwalDayOfWeek.Monday or JadwalDayOfWeek.Tuesday or JadwalDayOfWeek.Wednesday or JadwalDayOfWeek.Thursday or JadwalDayOfWeek.Friday:
            {
                // Find Period 5
                var p5Index = -1;
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].Kind == ScheduleTimelineItemKind.ClassPeriod &&
                        items[i].Period?.PeriodName?.Contains("5") == true)
                    {
                        p5Index = i;
                        break;
                    }
                }

                if (p5Index >= 0)
                {
                    var splitPoint = p5Index + 1;
                    return (items.Take(splitPoint).ToList().AsReadOnly(), items.Skip(splitPoint).ToList().AsReadOnly());
                }
                break;
            }

            case JadwalDayOfWeek.Saturday:
            {
                // On Saturday: Row 1 contains Period 4 plus the Recess break after Period 4.
                // Row 2 begins with Period 5 and continues through Period 8.
                var p4Index = -1;
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].Kind == ScheduleTimelineItemKind.ClassPeriod &&
                        items[i].Period?.PeriodName?.Contains("4") == true)
                    {
                        p4Index = i;
                        break;
                    }
                }

                if (p4Index >= 0)
                {
                    var splitPoint = p4Index + 1;
                    if (splitPoint < items.Count && items[splitPoint].Kind == ScheduleTimelineItemKind.BreakBlock)
                    {
                        splitPoint++;
                    }
                    return (items.Take(splitPoint).ToList().AsReadOnly(), items.Skip(splitPoint).ToList().AsReadOnly());
                }
                break;
            }

            case JadwalDayOfWeek.Sunday:
                return (items, Array.Empty<ScheduleTimelineItem>());
        }

        // Fallback: split in middle
        var mid = (items.Count + 1) / 2;
        return (items.Take(mid).ToList().AsReadOnly(), items.Skip(mid).ToList().AsReadOnly());
    }

    public static ThreeRowDaySchedule BuildThreeRowSchedule(
        IEnumerable<PeriodOccurrence> periods,
        JadwalDayOfWeek day,
        TimeOnly? currentTime = null)
    {
        var now = currentTime ?? TimeOnly.FromDateTime(DateTime.Now);
        var sorted = periods
            .OrderBy(p => ParseMinutes(p.StartTime) ?? 0)
            .ToList();

        ScheduleTimelineItem? ptItem = null;
        var academicPeriods = new List<ScheduleTimelineItem>();

        foreach (var p in sorted)
        {
            var status = p.ChangeRecord != null
                ? ClassLiveStatus.Changed
                : ClassLiveStatus.Compute(p.StartTime, p.EndTime, now);

            var isPt = p.PeriodName?.Contains("PT", StringComparison.OrdinalIgnoreCase) == true ||
                       p.Subject?.Contains("Physical", StringComparison.OrdinalIgnoreCase) == true ||
                       p.Subject?.Contains("رياضة", StringComparison.OrdinalIgnoreCase) == true;

            // Physical Education period is isolated in its slot; on Friday it is explicitly omitted
            if (isPt && day != JadwalDayOfWeek.Friday)
            {
                ptItem = ScheduleTimelineItem.ForPeriod(p, status);
            }
            else if (isPt && day == JadwalDayOfWeek.Friday)
            {
                // Friday: slot is completely removed
                continue;
            }
            else
            {
                academicPeriods.Add(ScheduleTimelineItem.ForPeriod(p, status));
            }
        }

        var row1 = new List<ScheduleTimelineItem>();
        var row2 = new List<ScheduleTimelineItem>();
        var row3 = new List<ScheduleTimelineItem>();
        BreakBarInfo? break1 = null;
        BreakBarInfo? break2 = null;

        if (day == JadwalDayOfWeek.Saturday)
        {
            // Saturday Half-Day: 8 periods total (Periods 1-4 in Row 1, Recess, Periods 5-8 in Row 2)
            row1.AddRange(academicPeriods.Take(4));
            row2.AddRange(academicPeriods.Skip(4).Take(4));

            var endRow1 = row1.LastOrDefault()?.Period?.EndTime ?? "10:35";
            var startRow2 = row2.FirstOrDefault()?.Period?.StartTime ?? "10:55";
            var endMin = ParseMinutes(endRow1) ?? 635;
            var startMin = ParseMinutes(startRow2) ?? 655;
            var dur = Math.Max(0, startMin - endMin);

            break1 = new BreakBarInfo(
                Id: "break_saturday_recess",
                Name: "Morning Recess Break",
                NamaazNote: "Mid-Morning Refreshment & Study Preparation",
                StartTime: endRow1,
                EndTime: startRow2,
                DurationMinutes: dur > 0 ? dur : 20,
                Icon: "☕"
            );
        }
        else if (academicPeriods.Count > 0)
        {
            // Monday - Friday (Standard 3-Row Grid: 3 Subject Cards per row)
            // Row 1: Periods 2, 3, 4 (3 cards) + small Physical Education slot (Mon-Thu)
            row1.AddRange(academicPeriods.Take(3));

            // Row 2: Periods 5, 6, 7 (3 cards)
            row2.AddRange(academicPeriods.Skip(3).Take(3));

            // Row 3: Periods 8, 9, 10 (3 cards)
            row3.AddRange(academicPeriods.Skip(6).Take(3));

            // Break 1: Recess Break between Row 1 and Row 2
            var endRow1 = row1.LastOrDefault()?.Period?.EndTime ?? "10:35";
            var startRow2 = row2.FirstOrDefault()?.Period?.StartTime ?? "10:55";
            var endMin1 = ParseMinutes(endRow1) ?? 635;
            var startMin2 = ParseMinutes(startRow2) ?? 655;
            var dur1 = Math.Max(0, startMin2 - endMin1);

            break1 = new BreakBarInfo(
                Id: "break_recess",
                Name: "Morning Recess Break",
                NamaazNote: "Refreshment & Academic Preparation",
                StartTime: endRow1,
                EndTime: startRow2,
                DurationMinutes: dur1 > 0 ? dur1 : 20,
                Icon: "☕"
            );

            // Break 2: Lunch & Namaz Break between Row 2 and Row 3
            if (row3.Count > 0)
            {
                var endRow2 = row2.LastOrDefault()?.Period?.EndTime ?? "12:40";
                var startRow3 = row3.FirstOrDefault()?.Period?.StartTime ?? "14:00";
                var endMin2 = ParseMinutes(endRow2) ?? 760;
                var startMin3 = ParseMinutes(startRow3) ?? 840;
                var dur2 = Math.Max(0, startMin3 - endMin2);

                var isFriday = day == JadwalDayOfWeek.Friday;
                break2 = new BreakBarInfo(
                    Id: isFriday ? "break_jumua" : "break_lunch_namaz",
                    Name: isFriday ? "Jumua Mubarak • Namaz & Lunch Break" : "Lunch & Namaz Break",
                    NamaazNote: isFriday ? "🕌 Jumua Namaz in Masjid" : "🕌 Zohr Namaaz • 1:15 PM",
                    StartTime: endRow2,
                    EndTime: startRow3,
                    DurationMinutes: dur2 > 0 ? dur2 : 80,
                    Icon: isFriday ? "🕌" : "☀️"
                );
            }
        }

        return new ThreeRowDaySchedule(
            Day: day,
            PhysicalEducationItem: ptItem,
            Row1Items: row1.AsReadOnly(),
            Break1: break1,
            Row2Items: row2.AsReadOnly(),
            Break2: break2,
            Row3Items: row3.AsReadOnly()
        );
    }
}

public record BreakBarInfo(
    string Id,
    string Name,
    string NamaazNote,
    string StartTime,
    string EndTime,
    int DurationMinutes,
    string Icon
)
{
    public string TimeRangeFormatted => $"{PeriodOccurrence.FormatTo12Hour(StartTime)} – {PeriodOccurrence.FormatTo12Hour(EndTime)}";
    public string DurationFormatted => ScheduleTimelineBuilder.FormatDuration(DurationMinutes);
}

public record ThreeRowDaySchedule(
    JadwalDayOfWeek Day,
    ScheduleTimelineItem? PhysicalEducationItem,
    IReadOnlyList<ScheduleTimelineItem> Row1Items,
    BreakBarInfo? Break1,
    IReadOnlyList<ScheduleTimelineItem> Row2Items,
    BreakBarInfo? Break2,
    IReadOnlyList<ScheduleTimelineItem> Row3Items
)
{
    public bool HasPhysicalEducation => PhysicalEducationItem != null && Day != JadwalDayOfWeek.Friday;
    public bool HasRow3 => Row3Items.Count > 0;
}

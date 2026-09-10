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
}

using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.DTOs;

public record DashboardSummaryDto(
    int TotalTasksCount,
    int PendingTasksCount,
    int CompletedTasksCount,
    int ClassesTodayCount,
    int CompletedClassesTodayCount,
    PeriodOccurrence? ActiveClass,
    PeriodOccurrence? NextClass,
    IReadOnlyList<TaskItem> RecentTasks
);

public record MenuBarSummaryDto(
    PeriodOccurrence? NextOrCurrentClass,
    IReadOnlyList<TaskItem> TopPendingTasks,
    int OverdueTasksCount,
    bool HasScheduleChanges,
    string HeaderSubtitle
);

public record TodayClassTimetableDto(
    JadwalDayOfWeek Day,
    DateOnly Date,
    TimeOnly CurrentTime,
    DayScheduleRule Rule,
    IReadOnlyList<ScheduleTimelineItem> Row1Items,
    IReadOnlyList<ScheduleTimelineItem> Row2Items,
    IReadOnlyList<TimetableChangeRecord> ActiveChanges,
    PeriodOccurrence? ActivePeriod,
    PeriodOccurrence? NextPeriod
);

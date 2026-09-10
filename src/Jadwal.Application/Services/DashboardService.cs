using Jadwal.Application.DTOs;
using Jadwal.Application.Interfaces;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public class DashboardService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IChangeRepository _changeRepository;
    private readonly ITimeProvider _timeProvider;

    public DashboardService(
        ITimetableRepository timetableRepository,
        ITaskRepository taskRepository,
        IChangeRepository changeRepository,
        ITimeProvider timeProvider)
    {
        _timetableRepository = timetableRepository;
        _taskRepository = taskRepository;
        _changeRepository = changeRepository;
        _timeProvider = timeProvider;
    }

    public async Task<TodayClassTimetableDto> GetTodayClassTimetableAsync(
        JadwalDayOfWeek? overrideDay = null,
        CancellationToken ct = default)
    {
        var targetDay = overrideDay ?? _timeProvider.CurrentDayOfWeek;
        var rule = DayScheduleRule.RuleFor(targetDay);
        var snapshot = await _timetableRepository.GetLatestSnapshotAsync(ct);
        var dayPeriods = snapshot?.GetPeriodsForDay(targetDay) ?? Array.Empty<PeriodOccurrence>();

        var currentTime = _timeProvider.CurrentTime;
        var timeline = ScheduleTimelineBuilder.BuildTimeline(dayPeriods, currentTime);
        var (row1, row2) = ScheduleTimelineBuilder.SplitIntoTwoHorizontalRows(timeline, targetDay);
        var threeRowSchedule = ScheduleTimelineBuilder.BuildThreeRowSchedule(dayPeriods, targetDay, currentTime);

        var changes = await _changeRepository.GetChangesAsync(ct);
        var activeChanges = changes.Where(c => !c.IsAcknowledged).ToList().AsReadOnly();

        PeriodOccurrence? activePeriod = null;
        PeriodOccurrence? nextPeriod = null;

        foreach (var item in timeline)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                if (item.Status.Kind == ClassStatusKind.InProgress && activePeriod == null)
                {
                    activePeriod = item.Period;
                }
                else if (item.Status.Kind == ClassStatusKind.StartingSoon || (item.Status.Kind == ClassStatusKind.Upcoming && nextPeriod == null))
                {
                    nextPeriod ??= item.Period;
                }
            }
        }

        return new TodayClassTimetableDto(
            Day: targetDay,
            Date: _timeProvider.CurrentDate,
            CurrentTime: currentTime,
            Rule: rule,
            Row1Items: row1,
            Row2Items: row2,
            ActiveChanges: activeChanges,
            ActivePeriod: activePeriod,
            NextPeriod: nextPeriod,
            ThreeRowSchedule: threeRowSchedule
        );
    }

    public async Task<DashboardSummaryDto> GetTodayDashboardAsync(CancellationToken ct = default)
    {
        var tasks = await _taskRepository.GetAllTasksAsync(ct);
        var todayClasses = await GetTodayClassTimetableAsync(null, ct);

        var totalTasks = tasks.Count;
        var pendingTasks = tasks.Count(t => !t.IsCompleted);
        var completedTasks = tasks.Count(t => t.IsCompleted);

        var allPeriods = todayClasses.Row1Items.Concat(todayClasses.Row2Items)
            .Where(i => i.Kind == ScheduleTimelineItemKind.ClassPeriod)
            .Select(i => i.Period!)
            .ToList();

        var classesToday = allPeriods.Count;
        var completedClasses = todayClasses.Row1Items.Concat(todayClasses.Row2Items)
            .Count(i => i.Kind == ScheduleTimelineItemKind.ClassPeriod && i.Status.Kind == ClassStatusKind.Completed);

        return new DashboardSummaryDto(
            TotalTasksCount: totalTasks,
            PendingTasksCount: pendingTasks,
            CompletedTasksCount: completedTasks,
            ClassesTodayCount: classesToday,
            CompletedClassesTodayCount: completedClasses,
            ActiveClass: todayClasses.ActivePeriod,
            NextClass: todayClasses.NextPeriod,
            RecentTasks: tasks.Take(10).ToList().AsReadOnly()
        );
    }

    public async Task<MenuBarSummaryDto> GetMenuBarSummaryAsync(CancellationToken ct = default)
    {
        var todayClasses = await GetTodayClassTimetableAsync(null, ct);
        var allTasks = await _taskRepository.GetAllTasksAsync(ct);

        var nextOrCurrent = todayClasses.ActivePeriod ?? todayClasses.NextPeriod;
        var pending = allTasks.Where(t => !t.IsCompleted).Take(4).ToList().AsReadOnly();

        var now = _timeProvider.UtcNow;
        var overdue = allTasks.Count(t => !t.IsCompleted && t.Deadline.HasValue && t.Deadline.Value < now);

        return new MenuBarSummaryDto(
            NextOrCurrentClass: nextOrCurrent,
            TopPendingTasks: pending,
            OverdueTasksCount: overdue,
            HasScheduleChanges: todayClasses.ActiveChanges.Count > 0,
            HeaderSubtitle: $"{todayClasses.Day.ToEnglishString()} • {todayClasses.Rule.DaySubtitle}"
        );
    }
}

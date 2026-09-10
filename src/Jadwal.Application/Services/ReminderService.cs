using Jadwal.Application.Interfaces;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public class ReminderService
{
    private readonly INotificationService? _notificationService;
    private readonly ITimeProvider _timeProvider;

    public ReminderService(ITimeProvider timeProvider, INotificationService? notificationService = null)
    {
        _timeProvider = timeProvider;
        _notificationService = notificationService;
    }

    public IReadOnlyList<ReminderItem> GenerateClassReminders(
        IEnumerable<PeriodOccurrence> periods,
        DateOnly targetDate,
        int alertMinutesBefore = 10)
    {
        var reminders = new List<ReminderItem>();

        foreach (var period in periods)
        {
            if (TimeOnly.TryParse(period.StartTime, out var startTime))
            {
                var triggerTime = targetDate.ToDateTime(startTime).AddMinutes(-alertMinutesBefore);
                if (triggerTime > _timeProvider.LocalNow)
                {
                    reminders.Add(new ReminderItem
                    {
                        PeriodId = period.Id,
                        TriggerTime = triggerTime,
                        Title = $"Class in {alertMinutesBefore}m: {period.PeriodName}",
                        Message = $"{period.Subject} at {period.StartTime} ({period.Details})"
                    });
                }
            }
        }

        return reminders.AsReadOnly();
    }

    public IReadOnlyList<ReminderItem> GenerateTaskReminders(IEnumerable<TaskItem> tasks)
    {
        var reminders = new List<ReminderItem>();
        var now = _timeProvider.UtcNow;

        foreach (var task in tasks)
        {
            if (!task.IsCompleted && task.Deadline.HasValue && task.Deadline.Value > now)
            {
                var triggerTime = task.Deadline.Value.AddHours(-1); // 1 hour before deadline
                if (triggerTime > now)
                {
                    reminders.Add(new ReminderItem
                    {
                        TaskId = task.Id,
                        TriggerTime = triggerTime,
                        Title = $"Task Due Soon: {task.Title}",
                        Message = $"Due at {task.Deadline.Value:g}"
                    });
                }
            }
        }

        return reminders.AsReadOnly();
    }
}

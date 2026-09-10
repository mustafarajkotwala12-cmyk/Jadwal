namespace Jadwal.Domain.Enums;

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum TaskCategory
{
    Academic = 0,
    Revision = 1,
    Hifz = 2,
    Homework = 3,
    General = 4
}

public enum ChangeType
{
    Added = 0,
    Removed = 1,
    TimeChanged = 2,
    SubjectChanged = 3,
    TeacherChanged = 4,
    RoomChanged = 5,
    Cancelled = 6
}

public enum ClassStatusKind
{
    Upcoming,
    StartingSoon,
    InProgress,
    Completed,
    Changed,
    Cancelled
}

public readonly record struct ClassLiveStatus(ClassStatusKind Kind, int MinutesRemaining = 0)
{
    public static ClassLiveStatus Upcoming => new(ClassStatusKind.Upcoming);
    public static ClassLiveStatus StartingSoon(int mins) => new(ClassStatusKind.StartingSoon, mins);
    public static ClassLiveStatus InProgress => new(ClassStatusKind.InProgress);
    public static ClassLiveStatus Completed => new(ClassStatusKind.Completed);
    public static ClassLiveStatus Changed => new(ClassStatusKind.Changed);
    public static ClassLiveStatus Cancelled => new(ClassStatusKind.Cancelled);

    public static ClassLiveStatus Compute(string startTime, string endTime, TimeOnly currentTime)
    {
        if (!TimeOnly.TryParse(startTime, out var start) || !TimeOnly.TryParse(endTime, out var end))
        {
            return Upcoming;
        }

        if (currentTime >= end)
        {
            return Completed;
        }

        if (currentTime >= start && currentTime < end)
        {
            return InProgress;
        }

        var minutesUntilStart = (int)(start.ToTimeSpan() - currentTime.ToTimeSpan()).TotalMinutes;
        if (minutesUntilStart > 0 && minutesUntilStart <= 15)
        {
            return StartingSoon(minutesUntilStart);
        }

        return Upcoming;
    }
}

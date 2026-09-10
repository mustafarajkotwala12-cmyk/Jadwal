using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Domain.Rules;

public class TimetableDiffEngine
{
    public IReadOnlyList<TimetableChangeRecord> Diff(TimetableSnapshot? oldSnapshot, TimetableSnapshot? newSnapshot)
    {
        if (newSnapshot == null) return Array.Empty<TimetableChangeRecord>();
        if (oldSnapshot == null)
        {
            // Initial import: no diff records needed
            return Array.Empty<TimetableChangeRecord>();
        }

        var oldMap = oldSnapshot.Periods.ToDictionary(p => p.Id, p => p);
        var newMap = newSnapshot.Periods.ToDictionary(p => p.Id, p => p);
        var changes = new List<TimetableChangeRecord>();

        // Check for modifications and additions
        foreach (var (newId, newPeriod) in newMap)
        {
            if (oldMap.TryGetValue(newId, out var oldPeriod))
            {
                // Check subject change
                if (!string.Equals(oldPeriod.Subject.Trim(), newPeriod.Subject.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(new TimetableChangeRecord
                    {
                        PeriodId = newId,
                        ChangeType = ChangeType.SubjectChanged,
                        OldSubject = oldPeriod.Subject,
                        NewSubject = newPeriod.Subject,
                        OldStartTime = oldPeriod.StartTime,
                        NewStartTime = newPeriod.StartTime,
                        OldEndTime = oldPeriod.EndTime,
                        NewEndTime = newPeriod.EndTime,
                        OldTeacher = oldPeriod.Details,
                        NewTeacher = newPeriod.Details
                    });
                }
                // Check time change
                else if (oldPeriod.StartTime != newPeriod.StartTime || oldPeriod.EndTime != newPeriod.EndTime)
                {
                    changes.Add(new TimetableChangeRecord
                    {
                        PeriodId = newId,
                        ChangeType = ChangeType.TimeChanged,
                        OldSubject = oldPeriod.Subject,
                        NewSubject = newPeriod.Subject,
                        OldStartTime = oldPeriod.StartTime,
                        NewStartTime = newPeriod.StartTime,
                        OldEndTime = oldPeriod.EndTime,
                        NewEndTime = newPeriod.EndTime,
                        OldTeacher = oldPeriod.Details,
                        NewTeacher = newPeriod.Details
                    });
                }
                // Check teacher/details change
                else if (!string.Equals(oldPeriod.Details.Trim(), newPeriod.Details.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(new TimetableChangeRecord
                    {
                        PeriodId = newId,
                        ChangeType = ChangeType.TeacherChanged,
                        OldSubject = oldPeriod.Subject,
                        NewSubject = newPeriod.Subject,
                        OldTeacher = oldPeriod.Details,
                        NewTeacher = newPeriod.Details
                    });
                }
            }
            else
            {
                // Added
                changes.Add(new TimetableChangeRecord
                {
                    PeriodId = newId,
                    ChangeType = ChangeType.Added,
                    NewSubject = newPeriod.Subject,
                    NewStartTime = newPeriod.StartTime,
                    NewEndTime = newPeriod.EndTime,
                    NewTeacher = newPeriod.Details
                });
            }
        }

        // Check for removals
        foreach (var (oldId, oldPeriod) in oldMap)
        {
            if (!newMap.ContainsKey(oldId))
            {
                changes.Add(new TimetableChangeRecord
                {
                    PeriodId = oldId,
                    ChangeType = ChangeType.Removed,
                    OldSubject = oldPeriod.Subject,
                    OldStartTime = oldPeriod.StartTime,
                    OldEndTime = oldPeriod.EndTime,
                    OldTeacher = oldPeriod.Details
                });
            }
        }

        return changes.AsReadOnly();
    }

    public TimetableSnapshot AnnotateSnapshot(TimetableSnapshot snapshot, IReadOnlyList<TimetableChangeRecord> changes)
    {
        var activeChangeMap = changes
            .Where(c => !c.IsAcknowledged)
            .GroupBy(c => c.PeriodId)
            .ToDictionary(g => g.Key, g => g.First());

        var updatedPeriods = new List<PeriodOccurrence>();
        foreach (var period in snapshot.Periods)
        {
            if (activeChangeMap.TryGetValue(period.Id, out var change))
            {
                updatedPeriods.Add(period with { ChangeRecord = change });
            }
            else
            {
                updatedPeriods.Add(period with { ChangeRecord = null });
            }
        }

        return snapshot with { Periods = updatedPeriods.AsReadOnly() };
    }
}

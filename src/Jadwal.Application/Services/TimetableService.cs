using Jadwal.Application.Interfaces;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Jadwal.Domain.Rules;

namespace Jadwal.Application.Services;

public class TimetableService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly IChangeRepository _changeRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IJamiaTimetableProvider _provider;
    private readonly TimetableDiffEngine _diffEngine = new();

    public TimetableService(
        ITimetableRepository timetableRepository,
        IChangeRepository changeRepository,
        ITaskRepository taskRepository,
        IJamiaTimetableProvider provider)
    {
        _timetableRepository = timetableRepository;
        _changeRepository = changeRepository;
        _taskRepository = taskRepository;
        _provider = provider;
    }

    public async Task<TimetableSnapshot?> GetLatestTimetableAsync(CancellationToken ct = default)
    {
        var snapshot = await _timetableRepository.GetLatestSnapshotAsync(ct);
        if (snapshot == null) return null;

        var changes = await _changeRepository.GetChangesAsync(ct);
        return _diffEngine.AnnotateSnapshot(snapshot, changes);
    }

    public async Task<IReadOnlyList<TimetableChangeRecord>> GetActiveChangesAsync(CancellationToken ct = default)
    {
        var changes = await _changeRepository.GetChangesAsync(ct);
        return changes.Where(c => !c.IsAcknowledged).ToList().AsReadOnly();
    }

    public async Task AcknowledgeAllChangesAsync(CancellationToken ct = default)
    {
        await _changeRepository.AcknowledgeAllChangesAsync(ct);
    }

    public async Task<bool> HasValidSessionAsync(CancellationToken ct = default)
    {
        return await _provider.HasValidSessionAsync(ct);
    }

    public async Task<IReadOnlyList<TimetableChangeRecord>> RefreshTimetableAsync(bool forceLogin = false, CancellationToken ct = default)
    {
        var freshRaw = await _provider.FetchCurrentTimetableAsync(forceLogin, ct);
        if (freshRaw == null)
        {
            throw new InvalidOperationException("Could not fetch timetable from Jamea Portal. Please verify your portal credentials or import a timetable file (.json / .xlsx).");
        }

        var oldSnapshot = await _timetableRepository.GetLatestSnapshotAsync(ct);
        var newChanges = _diffEngine.Diff(oldSnapshot, freshRaw);

        if (newChanges.Count > 0)
        {
            await _changeRepository.AppendChangesAsync(newChanges, ct);

            // Invariant: Protect tasks when subjects change or are removed
            var allTasks = await _taskRepository.GetAllTasksAsync(ct);
            foreach (var task in allTasks)
            {
                if (string.IsNullOrEmpty(task.LinkedPeriodId)) continue;

                var matchingChange = newChanges.FirstOrDefault(c => c.PeriodId == task.LinkedPeriodId);
                if (matchingChange != null)
                {
                    if (matchingChange.ChangeType == ChangeType.SubjectChanged && !string.IsNullOrEmpty(matchingChange.NewSubject))
                    {
                        var orig = task.LinkedSubject ?? matchingChange.OldSubject ?? "Unknown";
                        task.Discrepancy = new TaskDiscrepancy(orig, matchingChange.NewSubject, matchingChange.PeriodId);
                        await _taskRepository.SaveTaskAsync(task, ct);
                    }
                    else if (matchingChange.ChangeType == ChangeType.Removed || matchingChange.ChangeType == ChangeType.Cancelled)
                    {
                        var orig = task.LinkedSubject ?? matchingChange.OldSubject ?? "Unknown";
                        task.Discrepancy = new TaskDiscrepancy(orig, "Cancelled / Removed", matchingChange.PeriodId);
                        await _taskRepository.SaveTaskAsync(task, ct);
                    }
                }
            }
        }

        var allActive = await GetActiveChangesAsync(ct);
        var annotated = _diffEngine.AnnotateSnapshot(freshRaw, allActive);
        await _timetableRepository.SaveSnapshotAsync(annotated, ct);

        return newChanges;
    }

    public async Task<int> SaveImportedSnapshotAsync(TimetableSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot == null || snapshot.Periods.Count == 0)
        {
            throw new FormatException("The imported timetable contains no valid periods.");
        }
        await _timetableRepository.SaveSnapshotAsync(snapshot, ct);
        return snapshot.Periods.Count;
    }
}

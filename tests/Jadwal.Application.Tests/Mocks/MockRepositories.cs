using Jadwal.Application.Interfaces;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Tests.Mocks;

public class MockTimeProvider : ITimeProvider
{
    public DateTime LocalNow { get; set; } = new(2026, 9, 7, 7, 0, 0, DateTimeKind.Local);
    public DateTime UtcNow => LocalNow.ToUniversalTime();
    public TimeOnly CurrentTime => TimeOnly.FromDateTime(LocalNow);
    public DateOnly CurrentDate => DateOnly.FromDateTime(LocalNow);
    public JadwalDayOfWeek CurrentDayOfWeek => JadwalDayOfWeek.Monday;
}

public class InMemoryTaskRepository : ITaskRepository
{
    public List<TaskItem> Tasks { get; } = new();

    public Task<IReadOnlyList<TaskItem>> GetAllTasksAsync(CancellationToken ct = default)
    {
        return Task.FromResult((IReadOnlyList<TaskItem>)Tasks.ToList().AsReadOnly());
    }

    public Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id));
    }

    public Task SaveTaskAsync(TaskItem task, CancellationToken ct = default)
    {
        var existing = Tasks.FindIndex(t => t.Id == task.Id);
        if (existing >= 0)
            Tasks[existing] = task;
        else
            Tasks.Add(task);
        return Task.CompletedTask;
    }

    public Task DeleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        Tasks.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}

public class InMemoryTimetableRepository : ITimetableRepository
{
    public TimetableSnapshot? Snapshot { get; set; }

    public Task<TimetableSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Snapshot);
    }

    public Task SaveSnapshotAsync(TimetableSnapshot snapshot, CancellationToken ct = default)
    {
        Snapshot = snapshot;
        return Task.CompletedTask;
    }
}

public class InMemoryChangeRepository : IChangeRepository
{
    public List<TimetableChangeRecord> Changes { get; } = new();

    public Task<IReadOnlyList<TimetableChangeRecord>> GetChangesAsync(CancellationToken ct = default)
    {
        return Task.FromResult((IReadOnlyList<TimetableChangeRecord>)Changes.ToList().AsReadOnly());
    }

    public Task AppendChangesAsync(IEnumerable<TimetableChangeRecord> changes, CancellationToken ct = default)
    {
        Changes.AddRange(changes);
        return Task.CompletedTask;
    }

    public Task AcknowledgeChangeAsync(Guid changeId, CancellationToken ct = default)
    {
        var c = Changes.FirstOrDefault(x => x.Id == changeId);
        if (c != null) c.IsAcknowledged = true;
        return Task.CompletedTask;
    }

    public Task AcknowledgeAllChangesAsync(CancellationToken ct = default)
    {
        foreach (var c in Changes) c.IsAcknowledged = true;
        return Task.CompletedTask;
    }
}

public class MockJamiaTimetableProvider : IJamiaTimetableProvider
{
    public TimetableSnapshot? MockSnapshot { get; set; }

    public Task<bool> HasValidSessionAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>("mock_jwt_token");
    public Task<TimetableSnapshot?> FetchCurrentTimetableAsync(bool forceLogin = false, CancellationToken ct = default)
    {
        return Task.FromResult(MockSnapshot);
    }
}

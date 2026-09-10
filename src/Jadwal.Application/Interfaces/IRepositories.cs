using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Interfaces;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllTasksAsync(CancellationToken ct = default);
    Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveTaskAsync(TaskItem task, CancellationToken ct = default);
    Task DeleteTaskAsync(Guid id, CancellationToken ct = default);
}

public interface ITimetableRepository
{
    Task<TimetableSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default);
    Task SaveSnapshotAsync(TimetableSnapshot snapshot, CancellationToken ct = default);
}

public interface IChangeRepository
{
    Task<IReadOnlyList<TimetableChangeRecord>> GetChangesAsync(CancellationToken ct = default);
    Task AppendChangesAsync(IEnumerable<TimetableChangeRecord> changes, CancellationToken ct = default);
    Task AcknowledgeChangeAsync(Guid changeId, CancellationToken ct = default);
    Task AcknowledgeAllChangesAsync(CancellationToken ct = default);
}

public interface IProgramRepository
{
    Task<IReadOnlyList<ProgramItem>> GetAllProgramsAsync(CancellationToken ct = default);
    Task SaveProgramAsync(ProgramItem program, CancellationToken ct = default);
    Task DeleteProgramAsync(Guid id, CancellationToken ct = default);
}

public interface INoteRepository
{
    Task<IReadOnlyList<NoteItem>> GetAllNotesAsync(CancellationToken ct = default);
    Task SaveNoteAsync(NoteItem note, CancellationToken ct = default);
    Task DeleteNoteAsync(Guid id, CancellationToken ct = default);
}

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    TimeOnly CurrentTime { get; }
    DateOnly CurrentDate { get; }
    JadwalDayOfWeek CurrentDayOfWeek { get; }
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;
    public TimeOnly CurrentTime => TimeOnly.FromDateTime(DateTime.Now);
    public DateOnly CurrentDate => DateOnly.FromDateTime(DateTime.Now);
    public JadwalDayOfWeek CurrentDayOfWeek => DateTime.Now.DayOfWeek switch
    {
        DayOfWeek.Monday => JadwalDayOfWeek.Monday,
        DayOfWeek.Tuesday => JadwalDayOfWeek.Tuesday,
        DayOfWeek.Wednesday => JadwalDayOfWeek.Wednesday,
        DayOfWeek.Thursday => JadwalDayOfWeek.Thursday,
        DayOfWeek.Friday => JadwalDayOfWeek.Friday,
        DayOfWeek.Saturday => JadwalDayOfWeek.Saturday,
        DayOfWeek.Sunday => JadwalDayOfWeek.Sunday,
        _ => JadwalDayOfWeek.Monday
    };
}

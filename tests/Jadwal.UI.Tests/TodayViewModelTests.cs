using FluentAssertions;
using Jadwal.Application.DTOs;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Jadwal.UI.ViewModels;
using Xunit;

namespace Jadwal.UI.Tests;

public class TestTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow { get; set; } = new DateTime(2026, 9, 14, 8, 30, 0, DateTimeKind.Local);
    public TimeOnly CurrentTime => TimeOnly.FromDateTime(LocalNow);
    public DateOnly CurrentDate => DateOnly.FromDateTime(LocalNow);
    public JadwalDayOfWeek CurrentDayOfWeek { get; set; } = JadwalDayOfWeek.Monday;
}

public class MemoryTaskRepo : ITaskRepository
{
    private readonly List<TaskItem> _tasks = new();
    public Task<IReadOnlyList<TaskItem>> GetAllTasksAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskItem>>(_tasks.ToList());
    public Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));
    public Task SaveTaskAsync(TaskItem task, CancellationToken ct = default)
    {
        _tasks.RemoveAll(t => t.Id == task.Id);
        _tasks.Add(task);
        return Task.CompletedTask;
    }
    public Task DeleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        _tasks.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}

public class MemoryTimetableRepo : ITimetableRepository
{
    private TimetableSnapshot? _snapshot;
    public MemoryTimetableRepo(TimetableSnapshot? snapshot = null) => _snapshot = snapshot;
    public Task<TimetableSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default) => Task.FromResult(_snapshot);
    public Task SaveSnapshotAsync(TimetableSnapshot snapshot, CancellationToken ct = default)
    {
        _snapshot = snapshot;
        return Task.CompletedTask;
    }
}

public class MemoryChangeRepo : IChangeRepository
{
    private readonly List<TimetableChangeRecord> _changes = new();
    public Task<IReadOnlyList<TimetableChangeRecord>> GetChangesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TimetableChangeRecord>>(_changes.ToList());
    public Task AppendChangesAsync(IEnumerable<TimetableChangeRecord> changes, CancellationToken ct = default)
    {
        _changes.AddRange(changes);
        return Task.CompletedTask;
    }
    public Task AcknowledgeChangeAsync(Guid changeId, CancellationToken ct = default)
    {
        var found = _changes.FirstOrDefault(c => c.Id == changeId);
        if (found != null) found.IsAcknowledged = true;
        return Task.CompletedTask;
    }
    public Task AcknowledgeAllChangesAsync(CancellationToken ct = default)
    {
        foreach (var c in _changes) c.IsAcknowledged = true;
        return Task.CompletedTask;
    }
}

public class DummyJamiaProvider : IJamiaTimetableProvider
{
    public Task<bool> HasValidSessionAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>("dummy_token");
    public Task<TimetableSnapshot?> FetchCurrentTimetableAsync(bool forceLogin = false, CancellationToken ct = default) => Task.FromResult<TimetableSnapshot?>(null);
}

public class TodayViewModelTests
{
    private TimetableSnapshot CreateStandardSnapshot()
    {
        var periods = new List<PeriodOccurrence>
        {
            new("p1", JadwalDayOfWeek.Monday, "2026-09-14", "Period 1", "07:00", "07:45", "رياضيات", "Room 101"),
            new("p2", JadwalDayOfWeek.Monday, "2026-09-14", "Period 2", "07:45", "08:30", "علوم", "Lab A"),
            new("p3", JadwalDayOfWeek.Monday, "2026-09-14", "Period 3", "08:45", "09:30", "لغة عربية", "Room 101"), // 15m gap -> Recess
            new("p4", JadwalDayOfWeek.Monday, "2026-09-14", "Period 4", "09:30", "10:15", "تاريخ", "Room 101"),
            new("p5", JadwalDayOfWeek.Monday, "2026-09-14", "Period 5", "10:15", "11:00", "قرآن", "Hifz Hall"),
            new("p6", JadwalDayOfWeek.Monday, "2026-09-14", "Period 6", "12:30", "13:15", "فقه", "Room 102"), // 90m gap -> Lunch & Namaz
            new("p7", JadwalDayOfWeek.Monday, "2026-09-14", "Period 7", "13:15", "14:00", "أدب", "Room 102")
        };

        return new TimetableSnapshot("1446-1447", 1, periods, DateTime.UtcNow);
    }

    [Fact]
    public async Task TodayViewModel_LoadsSchedule_AndPopulatesTwoHorizontalRowsWithBreaks()
    {
        var snapshot = CreateStandardSnapshot();
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);
        var dashboardService = new DashboardService(timeRepo, taskRepo, changeRepo, timeProvider);

        var vm = new TodayViewModel(dashboardService, taskService, timetableService, timeProvider);

        await vm.RefreshScheduleAsync();

        vm.Row1Items.Should().NotBeEmpty();
        vm.Row2Items.Should().NotBeEmpty();

        // Row 1 should contain periods 1 to 5 and a break pill for the 15m gap between p2 and p3
        var breakPillsInRow1 = vm.Row1Items.OfType<BreakPillItemViewModel>().ToList();
        breakPillsInRow1.Should().NotBeEmpty();
        breakPillsInRow1[0].Break.DurationMinutes.Should().Be(15);

        // Row 2 should contain periods 6 and 7
        var classCardsInRow2 = vm.Row2Items.OfType<ClassCardItemViewModel>().ToList();
        classCardsInRow2.Should().HaveCount(2);
        classCardsInRow2[0].Period.Subject.Should().Be("فقه");
    }

    [Fact]
    public async Task ClassCardItemViewModel_FlipCard_TogglesTasksView()
    {
        var period = new PeriodOccurrence("p1", JadwalDayOfWeek.Monday, "2026-09-14", "Period 1", "07:00", "07:45", "فقه", "Room 101");
        var card = new ClassCardItemViewModel(period, ClassLiveStatus.Upcoming);

        card.IsFlipped.Should().BeFalse();
        card.ToggleFlip();
        card.IsFlipped.Should().BeTrue();
        card.ToggleFlip();
        card.IsFlipped.Should().BeFalse();
    }
}

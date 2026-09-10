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
    public static TimetableSnapshot CreateStandardSnapshot()
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

    [Fact]
    public void TodayViewModel_InitializesCurrentTimeStringImmediately()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);
        var dashboardService = new DashboardService(timeRepo, taskRepo, changeRepo, timeProvider);

        var vm = new TodayViewModel(dashboardService, taskService, timetableService, timeProvider);

        vm.CurrentTimeString.Should().NotBeNullOrWhiteSpace();
        vm.CurrentTimeString.Should().Contain("M"); // AM or PM
    }

    [Fact]
    public void TodayViewModel_OnTick_UpdatesCurrentTimeString()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);
        var dashboardService = new DashboardService(timeRepo, taskRepo, changeRepo, timeProvider);

        var vm = new TodayViewModel(dashboardService, taskService, timetableService, timeProvider);
        vm.CurrentTimeString = "Old Time";

        vm.OnTick();

        vm.CurrentTimeString.Should().NotBe("Old Time");
        vm.CurrentTimeString.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClassCardItemViewModel_AddTaskAsync_AddsTaskToServiceAndCard()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeProvider = new TestTimeProvider();
        var taskService = new TaskService(taskRepo, timeProvider);

        var period = new PeriodOccurrence(
            id: "p1",
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-14",
            periodName: "Period 1",
            startTime: "08:00",
            endTime: "08:45",
            subject: "الادب الفاطمي",
            details: "Room 101"
        );
        var status = new ClassLiveStatus(ClassStatusKind.Upcoming, 30);

        var card = new ClassCardItemViewModel(period, status, taskService);
        card.NewTaskTitle = "تحضير القصيدة";

        await card.AddTaskAsync();

        card.Tasks.Should().HaveCount(1);
        card.Tasks[0].Title.Should().Be("تحضير القصيدة");
        card.Tasks[0].LinkedSubject.Should().Be("الادب الفاطمي");
        card.TasksCountText.Should().Be("1 task");

        // Verify in repository
        var allTasks = await taskService.GetAllTasksAsync();
        allTasks.Should().HaveCount(1);
        allTasks[0].Title.Should().Be("تحضير القصيدة");
    }

    [Fact]
    public async Task ClassCardItemViewModel_ToggleAndDeletion_WorksCorrectly()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeProvider = new TestTimeProvider();
        var taskService = new TaskService(taskRepo, timeProvider);

        var period = new PeriodOccurrence(
            id: "p2",
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-14",
            periodName: "Period 2",
            startTime: "09:00",
            endTime: "09:45",
            subject: "كتاب الينبوع",
            details: "Room 102"
        );
        var status = new ClassLiveStatus(ClassStatusKind.Upcoming, 60);

        var card = new ClassCardItemViewModel(period, status, taskService);
        card.NewTaskTitle = "مراجعة الفصل الأول";
        await card.AddTaskAsync();

        var task = card.Tasks[0];
        task.IsCompleted.Should().BeFalse();

        // Toggle completion
        await card.ToggleTaskCompletionAsync(task);
        task.IsCompleted.Should().BeTrue();

        // Delete task
        await card.DeleteTaskAsync(task);
        card.Tasks.Should().BeEmpty();
        card.TasksCountText.Should().Be("0 tasks");

        var repoTasks = await taskService.GetAllTasksAsync();
        repoTasks.Should().BeEmpty();
    }

    [Fact]
    public void ClassCardItemViewModel_StatusTransitionsAndBadges_WorkAccurately()
    {
        var period = new PeriodOccurrence(
            id: "p1",
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-14",
            periodName: "Period 1",
            startTime: "08:30",
            endTime: "09:15",
            subject: "فقه",
            details: "Room 101"
        );

        var card = new ClassCardItemViewModel(period, ClassLiveStatus.Upcoming);

        // 1. Before class (Upcoming)
        card.UpdateStatus(new TimeOnly(7, 30));
        card.StatusText.Should().Be("UPCOMING");
        card.StatusBadgeBackground.Should().Be("#F8F4EC");
        card.StatusBadgeForeground.Should().Be("#8C6D37");

        // 2. 10 minutes before class (StartingSoon)
        card.UpdateStatus(new TimeOnly(8, 20));
        card.StatusText.Should().Be("IN 10M");
        card.StatusBadgeBackground.Should().Be("#CCE5FF");
        card.StatusBadgeForeground.Should().Be("#004085");

        // 3. During class (InProgress)
        card.UpdateStatus(new TimeOnly(8, 45));
        card.StatusText.Should().Be("IN SESSION");
        card.StatusBadgeBackground.Should().Be("#D4EDDA");
        card.StatusBadgeForeground.Should().Be("#155724");

        // 4. After class has ended (Completed)
        card.UpdateStatus(new TimeOnly(9, 30));
        card.StatusText.Should().Be("DONE");
        card.StatusBadgeBackground.Should().Be("#E9ECEF");
        card.StatusBadgeForeground.Should().Be("#495057");

        // 5. Changed Period (while active/upcoming)
        var changedPeriod = new PeriodOccurrence(
            id: "p2",
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-14",
            periodName: "Period 2",
            startTime: "09:30",
            endTime: "10:15",
            subject: "ادب",
            details: "Room 102",
            changeRecord: new TimetableChangeRecord
            {
                PeriodId = "p2",
                ChangeType = ChangeType.SubjectChanged,
                OldSubject = "قرآن",
                NewSubject = "ادب"
            }
        );
        var changedCard = new ClassCardItemViewModel(changedPeriod, ClassLiveStatus.Changed);
        changedCard.UpdateStatus(new TimeOnly(9, 35));
        changedCard.StatusText.Should().Be("CHANGED");
        changedCard.StatusBadgeBackground.Should().Be("#FFF3CD");
        changedCard.StatusBadgeForeground.Should().Be("#856404");

        // When changed class concludes, it reflects DONE
        changedCard.UpdateStatus(new TimeOnly(10, 30));
        changedCard.StatusText.Should().Be("DONE");
        changedCard.StatusBadgeBackground.Should().Be("#E9ECEF");
    }
}

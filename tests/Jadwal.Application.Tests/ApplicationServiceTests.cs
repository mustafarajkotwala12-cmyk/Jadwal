using FluentAssertions;
using Jadwal.Application.Services;
using Jadwal.Application.Tests.Mocks;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Xunit;

namespace Jadwal.Application.Tests;

public class ApplicationServiceTests
{
    [Fact]
    public async Task TaskService_CanCreateToggleAndDeleteTasks()
    {
        var taskRepo = new InMemoryTaskRepository();
        var timeProvider = new MockTimeProvider();
        var service = new TaskService(taskRepo, timeProvider);

        var task = await service.CreateTaskAsync(
            title: "Study Nahw chapter 2",
            notes: "Exercise 1 to 5",
            priority: TaskPriority.High,
            category: TaskCategory.Academic,
            linkedSubject: "Nahw",
            linkedPeriodId: "2026-09-07_Period_5"
        );

        task.Should().NotBeNull();
        task.Title.Should().Be("Study Nahw chapter 2");
        task.IsCompleted.Should().BeFalse();

        // Toggle to completed
        var toggled = await service.ToggleTaskCompletionAsync(task.Id);
        toggled!.IsCompleted.Should().BeTrue();
        toggled.CompletedAt.Should().NotBeNull();

        // Toggle back to reopen
        var reopened = await service.ToggleTaskCompletionAsync(task.Id);
        reopened!.IsCompleted.Should().BeFalse();

        // Delete
        await service.DeleteTaskAsync(task.Id);
        var all = await service.GetAllTasksAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task TimetableService_RefreshesAndProtectsTasksOnSubjectChange()
    {
        var taskRepo = new InMemoryTaskRepository();
        var timetableRepo = new InMemoryTimetableRepository();
        var changeRepo = new InMemoryChangeRepository();
        var timeProvider = new MockTimeProvider();
        var provider = new MockJamiaTimetableProvider();

        // Baseline timetable
        var baseline = new TimetableSnapshot("1447", 25, new List<PeriodOccurrence>
        {
            new("2026-09-07_Period_4", JadwalDayOfWeek.Monday, "2026-09-07", "Period 4", "10:00", "10:35", "Linguistics", "Room 101")
        });
        await timetableRepo.SaveSnapshotAsync(baseline);

        // User task linked to Period 4
        var task = new TaskItem("Essay on Linguistics", null, TaskPriority.High, TaskCategory.Academic, "Linguistics", "2026-09-07_Period_4");
        await taskRepo.SaveTaskAsync(task);

        // School updates timetable: Period 4 changed to Economics!
        provider.MockSnapshot = new TimetableSnapshot("1447", 25, new List<PeriodOccurrence>
        {
            new("2026-09-07_Period_4", JadwalDayOfWeek.Monday, "2026-09-07", "Period 4", "10:00", "10:35", "Economics", "Room 102")
        });

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, provider);
        var changes = await timetableService.RefreshTimetableAsync();

        changes.Should().HaveCount(1);
        changes.First().ChangeType.Should().Be(ChangeType.SubjectChanged);

        // Task must NOT be deleted, and must have discrepancy attached
        var updatedTask = await taskRepo.GetTaskByIdAsync(task.Id);
        updatedTask.Should().NotBeNull();
        updatedTask!.Title.Should().Be("Essay on Linguistics");
        updatedTask.Discrepancy.Should().NotBeNull();
        updatedTask.Discrepancy!.NewSubject.Should().Be("Economics");
        updatedTask.Discrepancy.OriginalSubject.Should().Be("Linguistics");
    }

    [Fact]
    public async Task DashboardService_CalculatesAccurateSummaries()
    {
        var taskRepo = new InMemoryTaskRepository();
        var timetableRepo = new InMemoryTimetableRepository();
        var changeRepo = new InMemoryChangeRepository();
        var timeProvider = new MockTimeProvider();

        // 2 tasks: 1 pending, 1 completed
        var t1 = new TaskItem("Task 1");
        var t2 = new TaskItem("Task 2");
        t2.MarkCompleted();
        await taskRepo.SaveTaskAsync(t1);
        await taskRepo.SaveTaskAsync(t2);

        // Timetable with morning and afternoon classes
        var periods = new List<PeriodOccurrence>
        {
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 2", "06:00", "06:45", "Fiqh", "R1"), // Completed relative to 7:00 AM
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 3", "08:35", "09:10", "Adab", "R2")  // Next
        };
        await timetableRepo.SaveSnapshotAsync(new TimetableSnapshot("1447", 25, periods));

        var dashboardService = new DashboardService(timetableRepo, taskRepo, changeRepo, timeProvider);
        var summary = await dashboardService.GetTodayDashboardAsync();

        summary.TotalTasksCount.Should().Be(2);
        summary.PendingTasksCount.Should().Be(1);
        summary.CompletedTasksCount.Should().Be(1);
        summary.ClassesTodayCount.Should().Be(2);

        var menuBar = await dashboardService.GetMenuBarSummaryAsync();
        menuBar.TopPendingTasks.Should().HaveCount(1);
        menuBar.TopPendingTasks.First().Title.Should().Be("Task 1");
    }

    [Fact]
    public void ReminderService_CalculatesUpcomingReminders()
    {
        var timeProvider = new MockTimeProvider();
        var reminderService = new ReminderService(timeProvider);

        var periods = new List<PeriodOccurrence>
        {
            new("p1", JadwalDayOfWeek.Monday, "2026-09-07", "Period 5", "10:55", "11:30", "Nahw", "R4")
        };

        var targetDate = new DateOnly(2026, 9, 7);
        var reminders = reminderService.GenerateClassReminders(periods, targetDate, alertMinutesBefore: 15);

        reminders.Should().HaveCount(1);
        reminders.First().PeriodId.Should().Be("p1");
        reminders.First().Title.Should().Contain("15m");
    }
}

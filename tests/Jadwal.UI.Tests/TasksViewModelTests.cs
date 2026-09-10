using FluentAssertions;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.UI.ViewModels;
using Xunit;

namespace Jadwal.UI.Tests;

public class TasksViewModelTests
{
    [Fact]
    public async Task TasksViewModel_CreateTask_And_FilterTasks()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TasksViewModel(taskService, timetableService);
        await vm.InitializeAsync();

        // Create Task
        vm.NewTaskTitle = "Study Fiqh Lesson 4";
        vm.NewTaskSubject = "فقه";
        vm.NewTaskPriority = TaskPriority.High;
        vm.NewTaskCategory = TaskCategory.Academic;
        await vm.CreateTaskAsync();

        vm.FilteredTasks.Should().HaveCount(1);
        vm.FilteredTasks[0].Title.Should().Be("Study Fiqh Lesson 4");
        vm.FilteredTasks[0].LinkedSubject.Should().Be("فقه");
        vm.FilteredTasks[0].Priority.Should().Be(TaskPriority.High);

        // Toggle complete
        await vm.ToggleTaskCompletionAsync(vm.FilteredTasks[0]);
        // Default filter is "Active", so completed task shouldn't appear
        vm.FilteredTasks.Should().BeEmpty();

        // Switch filter to "All"
        vm.SetFilterStatus("All");
        vm.FilteredTasks.Should().HaveCount(1);
        vm.FilteredTasks[0].IsCompleted.Should().BeTrue();

        // Delete task
        await vm.DeleteTaskAsync(vm.FilteredTasks[0]);
        vm.FilteredTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task TasksViewModel_EditTask_UpdatesTaskDetailsSuccessfully()
    {
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TasksViewModel(taskService, timetableService);
        await vm.InitializeAsync();

        // 1. Create an initial task
        vm.NewTaskTitle = "Original Task";
        vm.NewTaskSubject = "General";
        vm.NewTaskPriority = TaskPriority.Low;
        vm.NewTaskCategory = TaskCategory.Homework;
        vm.NewTaskNotes = "Initial note";
        await vm.CreateTaskAsync();

        vm.FilteredTasks.Should().HaveCount(1);
        var created = vm.FilteredTasks[0];

        // 2. Start editing
        vm.StartEditTask(created);
        vm.IsEditingTask.Should().BeTrue();
        vm.EditingTaskId.Should().Be(created.Id);
        vm.EditingTaskTitle.Should().Be("Original Task");
        vm.EditingTaskNotes.Should().Be("Initial note");

        // 3. Change details
        vm.EditingTaskTitle = "Updated Fiqh Assignment";
        vm.EditingTaskSubject = "فقه";
        vm.EditingTaskPriority = TaskPriority.Urgent;
        vm.EditingTaskCategory = TaskCategory.Revision;
        vm.EditingTaskNotes = "Revised comprehensive note";
        await vm.SaveEditedTaskAsync();

        // 4. Verify update
        vm.IsEditingTask.Should().BeFalse();
        vm.EditingTaskId.Should().BeNull();
        vm.FilteredTasks.Should().HaveCount(1);
        var updated = vm.FilteredTasks[0];
        updated.Title.Should().Be("Updated Fiqh Assignment");
        updated.LinkedSubject.Should().Be("فقه");
        updated.Priority.Should().Be(TaskPriority.Urgent);
        updated.Category.Should().Be(TaskCategory.Revision);
        updated.Notes.Should().Be("Revised comprehensive note");

        // 5. Test Cancel
        vm.StartEditTask(updated);
        vm.EditingTaskTitle = "Should Not Be Saved";
        vm.CancelEditTask();
        vm.IsEditingTask.Should().BeFalse();
        vm.FilteredTasks[0].Title.Should().Be("Updated Fiqh Assignment");
    }
}

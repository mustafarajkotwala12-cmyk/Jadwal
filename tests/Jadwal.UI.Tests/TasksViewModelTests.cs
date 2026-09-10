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
}

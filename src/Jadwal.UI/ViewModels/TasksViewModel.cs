using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public partial class TasksViewModel : ViewModelBase
{
    private readonly TaskService _taskService;
    private readonly TimetableService _timetableService;

    [ObservableProperty]
    private string _filterStatus = "Active"; // All, Active, Completed

    [ObservableProperty]
    private string _filterCategory = "All";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    // Add Task Form Fields
    [ObservableProperty]
    private bool _isAddingTask = false;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newTaskSubject = string.Empty;

    [ObservableProperty]
    private TaskPriority _newTaskPriority = TaskPriority.Medium;

    [ObservableProperty]
    private TaskCategory _newTaskCategory = TaskCategory.Academic;

    [ObservableProperty]
    private DateTimeOffset? _newTaskDeadline = DateTimeOffset.Now.AddDays(1);

    [ObservableProperty]
    private string _newTaskNotes = string.Empty;

    public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
    public ObservableCollection<string> AvailableSubjects { get; } = new();

    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        "Active", "All", "Completed"
    };

    public ObservableCollection<string> CategoryOptions { get; } = new()
    {
        "All", "Academic", "Hifz", "Homework", "Revision", "General"
    };

    public ObservableCollection<TaskPriority> PriorityOptions { get; } = new()
    {
        TaskPriority.Low, TaskPriority.Medium, TaskPriority.High, TaskPriority.Urgent
    };

    [RelayCommand]
    public void SetFilterStatus(string status)
    {
        FilterStatus = status;
    }

    public TasksViewModel(TaskService taskService, TimetableService timetableService)
    {
        _taskService = taskService;
        _timetableService = timetableService;
    }

    public async Task InitializeAsync()
    {
        await LoadSubjectsAsync();
        await RefreshTasksAsync();
    }

    private async Task LoadSubjectsAsync()
    {
        AvailableSubjects.Clear();
        AvailableSubjects.Add("General");
        var snapshot = await _timetableService.GetLatestTimetableAsync();
        if (snapshot != null)
        {
            var subjects = snapshot.Periods
                .Select(p => p.Subject)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s);

            foreach (var s in subjects) AvailableSubjects.Add(s);
        }
        NewTaskSubject = AvailableSubjects.FirstOrDefault() ?? "General";
    }

    partial void OnFilterStatusChanged(string value) => _ = RefreshTasksAsync();
    partial void OnFilterCategoryChanged(string value) => _ = RefreshTasksAsync();
    partial void OnSearchQueryChanged(string value) => _ = RefreshTasksAsync();

    [RelayCommand]
    public async Task RefreshTasksAsync()
    {
        var tasks = await _taskService.GetAllTasksAsync();

        var query = SearchQuery?.Trim() ?? string.Empty;

        var filtered = tasks.AsEnumerable();

        if (FilterStatus == "Active")
            filtered = filtered.Where(t => !t.IsCompleted);
        else if (FilterStatus == "Completed")
            filtered = filtered.Where(t => t.IsCompleted);

        if (FilterCategory != "All" && Enum.TryParse<TaskCategory>(FilterCategory, out var category))
        {
            filtered = filtered.Where(t => t.Category == category);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.LinkedSubject != null && t.LinkedSubject.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (t.Notes != null && t.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredTasks.Clear();
        foreach (var task in filtered.OrderByDescending(t => t.Priority).ThenBy(t => t.Deadline ?? DateTime.MaxValue))
        {
            FilteredTasks.Add(task);
        }
    }

    [RelayCommand]
    public void ToggleAddTask()
    {
        IsAddingTask = !IsAddingTask;
        if (IsAddingTask && string.IsNullOrEmpty(NewTaskSubject) && AvailableSubjects.Count > 0)
        {
            NewTaskSubject = AvailableSubjects[0];
        }
    }

    [RelayCommand]
    public async Task CreateTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle))
            return;

        await _taskService.CreateTaskAsync(
            title: NewTaskTitle.Trim(),
            notes: string.IsNullOrWhiteSpace(NewTaskNotes) ? null : NewTaskNotes.Trim(),
            priority: NewTaskPriority,
            category: NewTaskCategory,
            linkedSubject: string.IsNullOrEmpty(NewTaskSubject) ? null : NewTaskSubject,
            deadline: NewTaskDeadline?.DateTime
        );

        // Reset form
        NewTaskTitle = string.Empty;
        NewTaskNotes = string.Empty;
        IsAddingTask = false;

        await RefreshTasksAsync();
    }

    [RelayCommand]
    public async Task ToggleTaskCompletionAsync(TaskItem task)
    {
        if (task == null) return;
        await _taskService.ToggleTaskCompletionAsync(task.Id);
        await RefreshTasksAsync();
    }

    [RelayCommand]
    public async Task DeleteTaskAsync(TaskItem task)
    {
        if (task == null) return;
        await _taskService.DeleteTaskAsync(task.Id);
        await RefreshTasksAsync();
    }
}

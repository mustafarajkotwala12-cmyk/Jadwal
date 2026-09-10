using Jadwal.Application.Interfaces;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public enum DiscrepancyAction
{
    AdoptNewSubject,
    KeepOriginalContext,
    Dismiss
}

public class TaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITimeProvider _timeProvider;

    public TaskService(ITaskRepository taskRepository, ITimeProvider timeProvider)
    {
        _taskRepository = taskRepository;
        _timeProvider = timeProvider;
    }

    public async Task<TaskItem> CreateTaskAsync(
        string title,
        string? notes = null,
        TaskPriority priority = TaskPriority.Medium,
        TaskCategory category = TaskCategory.General,
        string? linkedSubject = null,
        string? linkedPeriodId = null,
        DateTime? deadline = null,
        CancellationToken ct = default)
    {
        var task = new TaskItem(title, notes, priority, category, linkedSubject, linkedPeriodId, deadline);
        await _taskRepository.SaveTaskAsync(task, ct);
        return task;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTasksAsync(CancellationToken ct = default)
    {
        return await _taskRepository.GetAllTasksAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksForPeriodAsync(
        string periodId,
        string? subject = null,
        CancellationToken ct = default)
    {
        var all = await _taskRepository.GetAllTasksAsync(ct);
        return all
            .Where(t => t.LinkedPeriodId == periodId || (!string.IsNullOrEmpty(subject) && t.LinkedSubject == subject))
            .ToList()
            .AsReadOnly();
    }

    public async Task<TaskItem?> ToggleTaskCompletionAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetTaskByIdAsync(id, ct);
        if (task == null) return null;

        if (task.IsCompleted)
            task.Reopen();
        else
            task.MarkCompleted();

        await _taskRepository.SaveTaskAsync(task, ct);
        return task;
    }

    public async Task UpdateTaskAsync(TaskItem task, CancellationToken ct = default)
    {
        await _taskRepository.SaveTaskAsync(task, ct);
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        await _taskRepository.DeleteTaskAsync(id, ct);
    }

    public async Task ResolveDiscrepancyAsync(Guid taskId, DiscrepancyAction action, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId, ct);
        if (task == null || task.Discrepancy == null) return;

        switch (action)
        {
            case DiscrepancyAction.AdoptNewSubject:
                task.AdoptNewSubject();
                break;
            case DiscrepancyAction.KeepOriginalContext:
                task.KeepOriginalContext();
                break;
            case DiscrepancyAction.Dismiss:
                task.DismissDiscrepancy();
                break;
        }

        await _taskRepository.SaveTaskAsync(task, ct);
    }
}

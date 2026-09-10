using System.Text.Json.Serialization;
using Jadwal.Domain.Enums;

namespace Jadwal.Domain.Models;

public record TaskDiscrepancy
{
    [JsonPropertyName("originalSubject")]
    public string OriginalSubject { get; init; } = string.Empty;

    [JsonPropertyName("newSubject")]
    public string NewSubject { get; init; } = string.Empty;

    [JsonPropertyName("periodId")]
    public string PeriodId { get; init; } = string.Empty;

    [JsonPropertyName("isDismissed")]
    public bool IsDismissed { get; set; } = false;

    public TaskDiscrepancy() { }

    public TaskDiscrepancy(string originalSubject, string newSubject, string periodId, bool isDismissed = false)
    {
        OriginalSubject = originalSubject;
        NewSubject = newSubject;
        PeriodId = periodId;
        IsDismissed = isDismissed;
    }
}

public class TaskItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("priority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [JsonPropertyName("category")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskCategory Category { get; set; } = TaskCategory.General;

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("deadline")]
    public DateTime? Deadline { get; set; }

    [JsonPropertyName("linkedSubject")]
    public string? LinkedSubject { get; set; }

    [JsonPropertyName("linkedPeriodId")]
    public string? LinkedPeriodId { get; set; }

    [JsonPropertyName("discrepancy")]
    public TaskDiscrepancy? Discrepancy { get; set; }

    public TaskItem() { }

    public TaskItem(
        string title,
        string? notes = null,
        TaskPriority priority = TaskPriority.Medium,
        TaskCategory category = TaskCategory.General,
        string? linkedSubject = null,
        string? linkedPeriodId = null,
        DateTime? deadline = null)
    {
        Title = title;
        Notes = notes;
        Priority = priority;
        Category = category;
        LinkedSubject = linkedSubject;
        LinkedPeriodId = linkedPeriodId;
        Deadline = deadline;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedAt = null;
    }

    public void AdoptNewSubject()
    {
        if (Discrepancy != null)
        {
            LinkedSubject = Discrepancy.NewSubject;
            Discrepancy = null;
        }
    }

    public void KeepOriginalContext()
    {
        if (Discrepancy != null)
        {
            Discrepancy.IsDismissed = true;
        }
    }

    public void DismissDiscrepancy()
    {
        if (Discrepancy != null)
        {
            Discrepancy.IsDismissed = true;
        }
    }
}

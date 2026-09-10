using System.Text.Json.Serialization;
using Jadwal.Domain.Enums;

namespace Jadwal.Domain.Models;

public record TimetableChangeRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("periodId")]
    public string PeriodId { get; init; } = string.Empty;

    [JsonPropertyName("changeType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChangeType ChangeType { get; init; }

    [JsonPropertyName("oldSubject")]
    public string? OldSubject { get; init; }

    [JsonPropertyName("newSubject")]
    public string? NewSubject { get; init; }

    [JsonPropertyName("oldStartTime")]
    public string? OldStartTime { get; init; }

    [JsonPropertyName("newStartTime")]
    public string? NewStartTime { get; init; }

    [JsonPropertyName("oldEndTime")]
    public string? OldEndTime { get; init; }

    [JsonPropertyName("newEndTime")]
    public string? NewEndTime { get; init; }

    [JsonPropertyName("oldTeacher")]
    public string? OldTeacher { get; init; }

    [JsonPropertyName("newTeacher")]
    public string? NewTeacher { get; init; }

    [JsonPropertyName("oldRoom")]
    public string? OldRoom { get; init; }

    [JsonPropertyName("newRoom")]
    public string? NewRoom { get; init; }

    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("isAcknowledged")]
    public bool IsAcknowledged { get; set; } = false;

    [JsonIgnore]
    public string SummaryMessage => ChangeType switch
    {
        ChangeType.SubjectChanged => $"Subject changed from '{OldSubject}' to '{NewSubject}'",
        ChangeType.TimeChanged => $"Time shifted from {OldStartTime}-{OldEndTime} to {NewStartTime}-{NewEndTime}",
        ChangeType.Added => $"New class session added: {NewSubject}",
        ChangeType.Removed => $"Class session removed: {OldSubject}",
        ChangeType.Cancelled => $"Class session cancelled: {OldSubject}",
        ChangeType.TeacherChanged => $"Teacher updated: {NewTeacher}",
        ChangeType.RoomChanged => $"Location changed to: {NewRoom}",
        _ => "Schedule adjustment"
    };
}

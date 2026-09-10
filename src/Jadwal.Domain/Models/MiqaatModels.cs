using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jadwal.Domain.Models;

/// <summary>
/// Represents a specific historical or religious Miqaat event.
/// </summary>
public record MiqaatItem
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "day"; // "day" or "night"

    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 3;

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    public bool IsNight => string.Equals(Phase, "night", System.StringComparison.OrdinalIgnoreCase);
    public string PhaseBadge => IsNight ? "🌙 Night" : "☀️ Day";
}

/// <summary>
/// Represents the collection of miqaats associated with a specific Hijri month (0-11) and day (1-30).
/// </summary>
public record DayMiqaatsRecord
{
    [JsonPropertyName("month")]
    public int Month { get; init; } // 0-indexed (0 = Moharram, 11 = Zilhaj)

    [JsonPropertyName("date")]
    public int Date { get; init; } // 1-30

    [JsonPropertyName("miqaats")]
    public List<MiqaatItem> Miqaats { get; init; } = new();
}

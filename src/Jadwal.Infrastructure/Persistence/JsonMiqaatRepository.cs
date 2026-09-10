using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jadwal.Application.Interfaces;
using Jadwal.Domain.Models;

namespace Jadwal.Infrastructure.Persistence;

public class JsonMiqaatRepository : IMiqaatRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<DayMiqaatsRecord>? _cachedRecords;

    public async Task<IReadOnlyList<DayMiqaatsRecord>> GetAllMiqaatsAsync(CancellationToken ct = default)
    {
        if (_cachedRecords != null)
        {
            return _cachedRecords;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedRecords != null)
            {
                return _cachedRecords;
            }

            var records = await LoadRecordsAsync(ct);
            _cachedRecords = records;
            return _cachedRecords;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDayAsync(int monthZeroIndexed, int day, CancellationToken ct = default)
    {
        var all = await GetAllMiqaatsAsync(ct);
        var match = all.FirstOrDefault(r => r.Month == monthZeroIndexed && r.Date == day);
        if (match != null && match.Miqaats != null && match.Miqaats.Count > 0)
        {
            return match.Miqaats.AsReadOnly();
        }

        return Array.Empty<MiqaatItem>();
    }

    private static async Task<IReadOnlyList<DayMiqaatsRecord>> LoadRecordsAsync(CancellationToken ct)
    {
        // 1. Search file candidates
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "miqaats.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "miqaats.json"),
            Path.Combine(AppContext.BaseDirectory, "miqaats.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data", "miqaats.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "Data", "miqaats.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "miqaats.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "miqaats.json")
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    await using var stream = File.OpenRead(path);
                    var items = await JsonSerializer.DeserializeAsync<List<DayMiqaatsRecord>>(stream, JsonOptions, ct);
                    if (items != null && items.Count > 0)
                    {
                        return items.AsReadOnly();
                    }
                }
                catch
                {
                    // Fallthrough to next candidate
                }
            }
        }

        // 2. Search embedded resource in assembly
        try
        {
            var assembly = typeof(JsonMiqaatRepository).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("miqaats.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var items = await JsonSerializer.DeserializeAsync<List<DayMiqaatsRecord>>(stream, JsonOptions, ct);
                    if (items != null && items.Count > 0)
                    {
                        return items.AsReadOnly();
                    }
                }
            }
        }
        catch
        {
            // Ignore and return empty
        }

        return Array.Empty<DayMiqaatsRecord>();
    }
}

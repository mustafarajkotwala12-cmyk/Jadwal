using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jadwal.Application.Models;

namespace Jadwal.Application.Services;

public class PrayerTimeService
{
    private const string CityFileName = "city_preference.json";
    private const string CacheFileName = "prayer_times_cache.json";
    private const string BaseUrl = "https://api.aajnodin.com/index.php/home";

    private readonly HttpClient _httpClient;
    private readonly string _baseDir;
    private readonly ConcurrentDictionary<string, PrayerTimings> _memoryCache = new();

    public static IReadOnlyList<CityOption> SupportedCities => CityOption.All;

    public event Action<CityOption>? CityChanged;

    public PrayerTimeService(HttpClient? httpClient = null, string? dataDirectory = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));

        _baseDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data");

        try
        {
            Directory.CreateDirectory(_baseDir);
        }
        catch { }

        LoadCacheFromDisk();
    }

    public CityOption GetSavedCity()
    {
        try
        {
            var path = Path.Combine(_baseDir, CityFileName);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (int.TryParse(text, out var cityId))
                {
                    var found = SupportedCities.FirstOrDefault(c => c.Id == cityId);
                    if (found != null) return found;
                }
            }
        }
        catch { }

        // Default city: Mumbai (116)
        return CityOption.Mumbai;
    }

    public void SaveCity(CityOption city)
    {
        try
        {
            var path = Path.Combine(_baseDir, CityFileName);
            File.WriteAllText(path, city.Id.ToString());
            CityChanged?.Invoke(city);
        }
        catch { }
    }

    public async Task<PrayerTimings> GetPrayerTimingsAsync(
        DateOnly date,
        CityOption? city = null,
        CancellationToken ct = default)
    {
        var targetCity = city ?? GetSavedCity();
        var cacheKey = $"{targetCity.Id}_{date:yyyy-MM-dd}";

        if (_memoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var fetched = await FetchFromAajNoDinAsync(date, targetCity, ct);
            if (fetched != null)
            {
                _memoryCache[cacheKey] = fetched;
                SaveCacheToDisk();
                return fetched;
            }
        }
        catch
        {
            // Network failure or offline — fall back to defaults
        }

        // Offline / failure fallback
        var fallback = CreateDefaultTimings(date, targetCity);
        _memoryCache[cacheKey] = fallback;
        return fallback;
    }

    private async Task<PrayerTimings?> FetchFromAajNoDinAsync(DateOnly date, CityOption city, CancellationToken ct)
    {
        var url = $"{BaseUrl}?date={date:yyyy-MM-dd}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"city[name]={city.CookieCityName}; city[lat]={city.Latitude}; city[long]={city.Longitude}; city[tz]={city.Timezone}");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var html = await response.Content.ReadAsStringAsync(ct);

        // Extract Zawaal & Zohar End table cells
        var zawaalMatch = Regex.Match(html, @"Zawaal\s*</th>\s*<td[^>]*>\s*([^<]+)\s*</td>", RegexOptions.IgnoreCase);
        var zuhrEndMatch = Regex.Match(html, @"Zoh?ar\s*End\s*</th>\s*<td[^>]*>\s*([^<]+)\s*</td>", RegexOptions.IgnoreCase);

        if (!zawaalMatch.Success || !zuhrEndMatch.Success) return null;

        var rawZawaal = zawaalMatch.Groups[1].Value.Trim();
        var rawZuhrEnd = zuhrEndMatch.Groups[1].Value.Trim();

        var zawaal = FormatTimeString(rawZawaal);
        var zuhrEnd = FormatTimeString(rawZuhrEnd);

        return new PrayerTimings(date, city.Id, city.DisplayName, zawaal, zuhrEnd, DateTime.UtcNow);
    }

    private static string FormatTimeString(string raw)
    {
        var match = Regex.Match(raw, @"^0?(\d{1,2}):(\d{2})\s*(am|pm)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var h = match.Groups[1].Value;
            var m = match.Groups[2].Value;
            var ampm = match.Groups[3].Value.ToUpperInvariant();
            return $"{h}:{m} {ampm}";
        }
        return raw.ToUpperInvariant();
    }

    private static PrayerTimings CreateDefaultTimings(DateOnly date, CityOption city)
    {
        // Sensible astronomical defaults for Dawoodi Bohra centers if offline
        var zawaal = city.Id switch
        {
            124 => "12:04 PM", // Delhi
            321 or 320 => "12:15 PM", // East Africa
            365 => "12:30 PM", // Karachi
            145 => "12:22 PM", // Surat
            _ => "12:21 PM"    // Mumbai & default
        };

        var zuhrEnd = city.Id switch
        {
            124 => "1:53 PM",
            321 or 320 => "2:05 PM",
            365 => "2:20 PM",
            145 => "2:14 PM",
            _ => "2:15 PM"
        };

        return new PrayerTimings(date, city.Id, city.DisplayName, zawaal, zuhrEnd, DateTime.UtcNow);
    }

    private void LoadCacheFromDisk()
    {
        try
        {
            var path = Path.Combine(_baseDir, CacheFileName);
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<Dictionary<string, PrayerTimings>>(json);
            if (entries != null)
            {
                foreach (var (k, v) in entries)
                {
                    _memoryCache[k] = v;
                }
            }
        }
        catch { }
    }

    private void SaveCacheToDisk()
    {
        try
        {
            var path = Path.Combine(_baseDir, CacheFileName);
            var dict = _memoryCache.ToDictionary(k => k.Key, v => v.Value);
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }
}

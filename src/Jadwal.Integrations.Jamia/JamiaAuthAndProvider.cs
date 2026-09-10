using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Jadwal.Application.Interfaces;
using Jadwal.Domain.Models;

namespace Jadwal.Integrations.Jamia;

public static class JwtValidator
{
    public static bool IsTokenValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        try
        {
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expElement) && expElement.TryGetInt64(out var exp))
            {
                var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                // 60-second safety cushion
                return expTime > DateTimeOffset.UtcNow.AddSeconds(60);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class JamiaTimetableProvider : IJamiaTimetableProvider
{
    private readonly ISecureStorage? _secureStorage;
    private readonly TimetableImporter _importer = new();
    private readonly string _workspaceDirectory;

    public JamiaTimetableProvider(ISecureStorage? secureStorage = null, string? workspaceDirectory = null)
    {
        _secureStorage = secureStorage;
        _workspaceDirectory = workspaceDirectory ?? AppContext.BaseDirectory;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_secureStorage != null)
        {
            var token = await _secureStorage.GetSecretAsync("jamea_access_token", ct);
            if (JwtValidator.IsTokenValid(token)) return token;
        }

        // Check local token file
        var candidatePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data", "jamea_token.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "jamea_token.json"),
            Path.Combine(AppContext.BaseDirectory, "data", "jamea_token.json"),
            Path.Combine(AppContext.BaseDirectory, "jamea_token.json"),
            Path.Combine(_workspaceDirectory, "data", "jamea_token.json"),
            Path.Combine(_workspaceDirectory, "Data", "jamea_token.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "jamea_token.json")
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("access_token", out var tok))
                    {
                        var t = tok.GetString();
                        if (JwtValidator.IsTokenValid(t)) return t;
                    }
                }
                catch { }
            }
        }

        return null;
    }

    public async Task<bool> HasValidSessionAsync(CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        return !string.IsNullOrEmpty(token);
    }

    public async Task<TimetableSnapshot?> FetchCurrentTimetableAsync(bool forceLogin = false, CancellationToken ct = default)
    {
        // 1. If we have a valid token, try direct HTTPS API fetch (pure C#, no Python needed)
        var token = await GetAccessTokenAsync(ct);
        if (!string.IsNullOrEmpty(token))
        {
            var directSnapshot = await FetchViaDirectApiAsync(token, ct);
            if (directSnapshot != null)
            {
                return directSnapshot;
            }
        }

        // 2. Check candidate local data files
        var candidateDataPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data", "timetable.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "timetable.json"),
            Path.Combine(AppContext.BaseDirectory, "data", "timetable.json"),
            Path.Combine(AppContext.BaseDirectory, "timetable.json"),
            Path.Combine(_workspaceDirectory, "data", "timetable.json"),
            Path.Combine(_workspaceDirectory, "Data", "timetable.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "timetable.json")
        };

        // 2. Check candidate local data files as fallback
        foreach (var path in candidateDataPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return await _importer.ImportFromFileAsync(path, ct);
                }
                catch { }
            }
        }

        // 3. [LEGACY-BRIDGE]: When live browser login is needed on desktop
        return await ExecuteLegacyBridgeAsync(forceLogin, candidateDataPaths, ct);
    }

    private async Task<TimetableSnapshot?> FetchViaDirectApiAsync(string token, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Origin", "https://beta.jameasaifiyah.org");
            client.DefaultRequestHeaders.Add("Referer", "https://beta.jameasaifiyah.org/");
            client.DefaultRequestHeaders.Add("X-Menu-Id", "1372");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Extract claims from JWT
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
            var claimsJson = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var claimsDoc = JsonDocument.Parse(claimsJson);
            var root = claimsDoc.RootElement;

            int yearAr = 1448;
            if (root.TryGetProperty("yearAR", out var yProp))
            {
                if (yProp.ValueKind == JsonValueKind.Number) yearAr = yProp.GetInt32();
                else if (int.TryParse(yProp.GetString(), out var yParsed)) yearAr = yParsed;
            }

            string branchId = root.TryGetProperty("branchID", out var bProp) ? bProp.ToString() : "3";
            string classId = root.TryGetProperty("classID", out var cProp) ? cProp.ToString() : "3309";

            // 1. Get current week
            var weekReqBody = JsonSerializer.Serialize(new { yearAR = yearAr, branchID = branchId, teacherID = "%" });
            var weekResp = await client.PostAsync(
                "https://api.jameasaifiyah.org/api/JadwalPage/SelectWeekDDList_JadwalReports",
                new StringContent(weekReqBody, Encoding.UTF8, "application/json"), ct);

            if (!weekResp.IsSuccessStatusCode) return null;
            var weekJson = await weekResp.Content.ReadAsStringAsync(ct);
            using var weekDoc = JsonDocument.Parse(weekJson);

            int batchWeekNumber = 25;
            if (weekDoc.RootElement.TryGetProperty("data", out var weeksArray) && weeksArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in weeksArray.EnumerateArray())
                {
                    if (w.TryGetProperty("isCurrent", out var isCur) && isCur.GetBoolean())
                    {
                        if (w.TryGetProperty("batchWeekNumber", out var bwn))
                        {
                            batchWeekNumber = bwn.GetInt32();
                            break;
                        }
                    }
                }
            }

            // 2. Request Excel Generation
            var excelReqBody = JsonSerializer.Serialize(new
            {
                yearAR = yearAr,
                branchID = branchId,
                timeTablePeriodDayTypeID = 1,
                batchWeeKNumber = batchWeekNumber,
                type = "Class",
                classID = classId,
                teacherID = "%"
            });

            var excelResp = await client.PostAsync(
                "https://api.jameasaifiyah.org/api/JadwalReport/JadwalTimeTableReportExcel",
                new StringContent(excelReqBody, Encoding.UTF8, "application/json"), ct);

            if (!excelResp.IsSuccessStatusCode) return null;
            var excelJson = await excelResp.Content.ReadAsStringAsync(ct);
            using var excelDoc = JsonDocument.Parse(excelJson);

            string? downloadUrl = null;
            if (excelDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                dataObj.TryGetProperty("downloadUrl", out var dlProp))
            {
                downloadUrl = dlProp.GetString();
            }

            if (string.IsNullOrEmpty(downloadUrl)) return null;

            // 3. Download Excel bytes
            var dlResp = await client.GetAsync(downloadUrl, ct);
            if (!dlResp.IsSuccessStatusCode) return null;

            await using var excelStream = await dlResp.Content.ReadAsStreamAsync(ct);
            var snapshot = ExcelTimetableParser.Parse(excelStream);

            // Persist to local data/timetable.json for offline access
            try
            {
                var saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data");
                Directory.CreateDirectory(saveDir);
                var savePath = Path.Combine(saveDir, "timetable.json");
                await using var writeStream = File.Create(savePath);
                await JsonSerializer.SerializeAsync(writeStream, snapshot, new JsonSerializerOptions { WriteIndented = true }, ct);
            }
            catch { }

            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// [LEGACY-BRIDGE] Isolated subprocess execution of helper/jamea_helper.py.
    /// Redacts all tokens and passwords from diagnostics.
    /// </summary>
    private async Task<TimetableSnapshot?> ExecuteLegacyBridgeAsync(
        bool forceLogin,
        string[] candidateDataPaths,
        CancellationToken ct)
    {
        var scriptCandidates = new[]
        {
            Path.Combine(_workspaceDirectory, "helper", "jamea_helper.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "helper", "jamea_helper.py")
        };

        var scriptPath = scriptCandidates.FirstOrDefault(File.Exists);
        if (scriptPath == null) return null;

        var pythonExe = ResolvePythonExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = forceLogin ? $"\"{scriptPath}\" --login" : $"\"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)) ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);

        foreach (var path in candidateDataPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return await _importer.ImportFromFileAsync(path, ct);
                }
                catch { }
            }
        }

        return null;
    }

    private static string ResolvePythonExecutable()
    {
        var candidates = new[]
        {
            "/Library/Frameworks/Python.framework/Versions/3.13/bin/python3",
            "/Library/Frameworks/Python.framework/Versions/3.12/bin/python3",
            "/opt/homebrew/bin/python3",
            "/usr/local/bin/python3",
            "/usr/bin/python3",
            "python3",
            "python"
        };
        return candidates.FirstOrDefault(File.Exists) ?? "python3";
    }
}

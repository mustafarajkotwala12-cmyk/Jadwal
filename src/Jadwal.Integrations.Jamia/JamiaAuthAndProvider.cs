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

    public static string? GetTokenItsId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

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

            if (doc.RootElement.TryGetProperty("itsId", out var itsProp))
            {
                return itsProp.ValueKind == JsonValueKind.Number ? itsProp.GetInt64().ToString() : itsProp.GetString();
            }
            if (doc.RootElement.TryGetProperty("studentITSID", out var sItsProp))
            {
                return sItsProp.ValueKind == JsonValueKind.Number ? sItsProp.GetInt64().ToString() : sItsProp.GetString();
            }
            return null;
        }
        catch
        {
            return null;
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
        string? expectedItsId = null;
        if (_secureStorage != null)
        {
            expectedItsId = await _secureStorage.GetSecretAsync("its_id", ct);
            var token = await _secureStorage.GetSecretAsync("jamea_access_token", ct);
            if (JwtValidator.IsTokenValid(token))
            {
                var tokenItsId = JwtValidator.GetTokenItsId(token);
                if (string.IsNullOrEmpty(expectedItsId) || tokenItsId == expectedItsId)
                {
                    return token;
                }
            }
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
                        if (JwtValidator.IsTokenValid(t))
                        {
                            var tokenItsId = JwtValidator.GetTokenItsId(t);
                            if (string.IsNullOrEmpty(expectedItsId) || tokenItsId == expectedItsId)
                            {
                                return t;
                            }
                        }
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

        // 1. If we have a valid token matching our configured ITS ID and not explicitly forcing re-login, try direct HTTPS API fetch
        var token = await GetAccessTokenAsync(ct);
        if (!string.IsNullOrEmpty(token) && !forceLogin)
        {
            var directSnapshot = await FetchViaDirectApiAsync(token, ct);
            if (directSnapshot != null)
            {
                return directSnapshot;
            }
        }

        // 2. If token is missing, expired, belongs to another ITS ID, or fresh sync requested:
        // Attempt live browser/portal bridge execution
        try
        {
            var bridgeSnapshot = await ExecuteLegacyBridgeAsync(forceLogin || string.IsNullOrEmpty(token), candidateDataPaths, ct);
            if (bridgeSnapshot != null)
            {
                return bridgeSnapshot;
            }
        }
        catch (Exception ex)
        {
            if (forceLogin)
            {
                throw new InvalidOperationException($"Portal sync error: {ex.Message}", ex);
            }
        }

        // 3. If fresh sync was requested and bridge could not complete, fail informatively rather than showing stale 0 changes
        if (forceLogin)
        {
            throw new InvalidOperationException("Could not synchronize timetable from Jamia Portal. Please check your credentials or complete login in the browser window.");
        }

        // 4. Offline fallback: only use existing static data files if not explicitly performing a forced portal sync
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
        var scriptPath = ResolveHelperScriptPath(_workspaceDirectory);
        if (scriptPath == null)
        {
            throw new FileNotFoundException("Could not locate helper/jamea_helper.py in application package or workspace.");
        }

        var pythonExe = ResolvePythonExecutable();
        var workingDir = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)) ?? Directory.GetCurrentDirectory();

        string? itsId = null;
        string? itsPassword = null;
        if (_secureStorage != null)
        {
            itsId = await _secureStorage.GetSecretAsync("its_id", ct);
            itsPassword = await _secureStorage.GetSecretAsync("its_password", ct);
        }

        var arguments = forceLogin ? $"\"{scriptPath}\" --login" : $"\"{scriptPath}\"";
        if (!string.IsNullOrEmpty(itsId))
        {
            arguments += $" --its-id \"{itsId}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var pythonDir = Path.GetDirectoryName(pythonExe) ?? "";
        var existingPath = psi.EnvironmentVariables.ContainsKey("PATH") ? psi.EnvironmentVariables["PATH"] : "";
        psi.EnvironmentVariables["PATH"] = $"{pythonDir}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:{existingPath}";

        if (!string.IsNullOrEmpty(itsId))
        {
            psi.EnvironmentVariables["ITS_ID"] = itsId;
        }
        if (!string.IsNullOrEmpty(itsPassword))
        {
            psi.EnvironmentVariables["ITS_PASSWORD"] = itsPassword;
        }

        if (workingDir.Contains(".app/Contents/Resources"))
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data");
            Directory.CreateDirectory(appDataDir);
            psi.EnvironmentVariables["JAMEA_DATA_DIR"] = appDataDir;
        }

        using var process = new Process { StartInfo = psi };
        var errorBuilder = new StringBuilder();
        var outputBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var err = errorBuilder.ToString().Trim();
            var outMsg = outputBuilder.ToString().Trim();
            var detail = !string.IsNullOrEmpty(err) ? err : outMsg;
            throw new InvalidOperationException($"Portal synchronization failed (exit code {process.ExitCode}): {detail}");
        }

        // Check if helper generated or updated token file; persist to secure storage
        try
        {
            var tokenFileCandidates = new[]
            {
                Path.Combine(workingDir, "data", "jamea_token.json"),
                Path.Combine(workingDir, "Data", "jamea_token.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "jamea_token.json")
            };
            foreach (var tf in tokenFileCandidates)
            {
                if (File.Exists(tf))
                {
                    var j = await File.ReadAllTextAsync(tf, ct);
                    using var d = JsonDocument.Parse(j);
                    if (d.RootElement.TryGetProperty("access_token", out var tok))
                    {
                        var tStr = tok.GetString();
                        if (!string.IsNullOrEmpty(tStr) && JwtValidator.IsTokenValid(tStr) && _secureStorage != null)
                        {
                            await _secureStorage.SetSecretAsync("jamea_access_token", tStr, ct);
                        }
                    }
                    break;
                }
            }
        }
        catch { }

        // Import the newly generated snapshot
        var freshFileCandidates = new[]
        {
            Path.Combine(workingDir, "data", "timetable.json"),
            Path.Combine(workingDir, "Data", "timetable.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "timetable.json")
        }.Concat(candidateDataPaths);

        foreach (var path in freshFileCandidates)
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

    private static string? ResolveHelperScriptPath(string workspaceDir)
    {
        var envDir = Environment.GetEnvironmentVariable("JAMEA_HELPER_DIR");
        if (!string.IsNullOrEmpty(envDir))
        {
            var p1 = Path.Combine(envDir, "helper", "jamea_helper.py");
            if (File.Exists(p1)) return p1;
            var p2 = Path.Combine(envDir, "jamea_helper.py");
            if (File.Exists(p2)) return p2;
        }

        var baseDir = AppContext.BaseDirectory;
        var directCandidates = new[]
        {
            Path.Combine(baseDir, "..", "Resources", "helper", "jamea_helper.py"),
            Path.Combine(baseDir, "Resources", "helper", "jamea_helper.py"),
            Path.Combine(baseDir, "helper", "jamea_helper.py"),
            Path.Combine(workspaceDir, "helper", "jamea_helper.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "helper", "jamea_helper.py")
        };

        foreach (var cand in directCandidates)
        {
            try
            {
                var full = Path.GetFullPath(cand);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        var searchBases = new[] { baseDir, Directory.GetCurrentDirectory() };
        foreach (var sb in searchBases)
        {
            var dir = new DirectoryInfo(sb);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var cand = Path.Combine(dir.FullName, "helper", "jamea_helper.py");
                if (File.Exists(cand)) return cand;
                dir = dir.Parent;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var known = new[]
        {
            Path.Combine(home, "JameaHelper", "helper", "jamea_helper.py"),
            "/Users/mustafarajkotwala/JameaHelper/helper/jamea_helper.py"
        };
        foreach (var k in known)
        {
            if (File.Exists(k)) return k;
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

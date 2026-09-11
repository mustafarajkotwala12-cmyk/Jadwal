using System.Net;
using System.Text;
using System.Text.Json;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Models;
using Jadwal.Domain.Models;
using Jadwal.Integrations.Jamia.Services;

namespace Jadwal.Integrations.Jamia;

public class JamiaAuthExpiredException : Exception
{
    public JamiaAuthExpiredException(string message) : base(message) { }
}

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

/// <summary>
/// Native C# Jamia timetable provider.
/// Completely replaces legacy Python helper execution by delegating authentication
/// to IJamiaAuthenticationService and utilizing direct HTTPS API endpoints for timetable Excel retrieval.
/// </summary>
public class JamiaTimetableProvider : IJamiaTimetableProvider
{
    private readonly IJamiaAuthenticationService _authService;
    private readonly IJamiaCredentialStore _credentialStore;
    private readonly TimetableImporter _importer = new();

    public JamiaTimetableProvider(ISecureStorage? secureStorage, string? workspaceDirectory = null)
        : this(null, null, secureStorage, workspaceDirectory)
    {
    }

    public JamiaTimetableProvider(IJamiaAuthenticationService authService, IJamiaCredentialStore? credentialStore = null)
        : this(authService, credentialStore, null, null)
    {
    }

    public JamiaTimetableProvider(
        IJamiaAuthenticationService? authService = null,
        IJamiaCredentialStore? credentialStore = null,
        ISecureStorage? secureStorage = null,
        string? workspaceDirectory = null)
    {
        _credentialStore = credentialStore ?? new JamiaCredentialStore(secureStorage);
        _authService = authService ?? new JamiaAuthenticationService(_credentialStore, new PlaywrightJamiaAuthenticator());
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var session = await _authService.TryGetValidSessionAsync(ct);
        return session?.AccessToken;
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "timetable.json"),
            Path.Combine(AppContext.BaseDirectory, "data", "timetable.json")
        };

        // 1. If not forcing a re-login, try to reuse existing valid credential
        if (!forceLogin)
        {
            var token = await GetAccessTokenAsync(ct);
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var directSnapshot = await FetchViaDirectApiAsync(token, ct);
                    if (directSnapshot != null)
                    {
                        return directSnapshot;
                    }
                }
                catch (JamiaAuthExpiredException)
                {
                    // Session expired during API call; invalidate stored credential and proceed to single re-auth
                    await _authService.InvalidateAsync(ct);
                }
                catch (Exception)
                {
                    // Network or parser error; if offline, attempt fallback below
                }
            }
        }

        // 2. Need interactive login (missing token, expired, or forceLogin requested)
        try
        {
            var freshSession = await _authService.AuthenticateInteractiveAsync(ct);
            if (freshSession != null && !string.IsNullOrEmpty(freshSession.AccessToken))
            {
                var refreshedSnapshot = await FetchViaDirectApiAsync(freshSession.AccessToken, ct);
                if (refreshedSnapshot != null)
                {
                    return refreshedSnapshot;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // User closed browser window or canceled login.
            // Preserve existing local timetable data!
            throw;
        }
        catch (Exception ex)
        {
            if (forceLogin)
            {
                throw new InvalidOperationException($"Portal synchronization error: {ex.Message}", ex);
            }
        }

        // 3. Offline fallback: only use existing static data files if not explicitly performing a forced portal sync
        if (!forceLogin)
        {
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
        }

        return null;
    }

    private async Task<TimetableSnapshot?> FetchViaDirectApiAsync(string token, CancellationToken ct)
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

        if (weekResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new JamiaAuthExpiredException("Jamia portal session has expired (401/403).");
        }

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

        if (excelResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new JamiaAuthExpiredException("Jamia portal session has expired (401/403).");
        }

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
        if (dlResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new JamiaAuthExpiredException("Jamia portal session has expired (401/403).");
        }
        if (!dlResp.IsSuccessStatusCode) return null;

        await using var excelStream = await dlResp.Content.ReadAsStreamAsync(ct);
        var snapshot = ExcelTimetableParser.Parse(excelStream);

        // Persist to local data/timetable.json for offline access
        try
        {
            var saveDir = OperatingSystem.IsMacOS()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data");

            Directory.CreateDirectory(saveDir);
            var savePath = Path.Combine(saveDir, "timetable.json");
            await using var writeStream = File.Create(savePath);
            await JsonSerializer.SerializeAsync(writeStream, snapshot, new JsonSerializerOptions { WriteIndented = true }, ct);
        }
        catch { }

        return snapshot;
    }
}

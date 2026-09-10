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
            Path.Combine(_workspaceDirectory, "data", "jamea_token.json"),
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
        // 1. Check if Data/timetable.json is directly readable
        var candidateDataPaths = new[]
        {
            Path.Combine(_workspaceDirectory, "Data", "timetable.json"),
            Path.Combine(_workspaceDirectory, "data", "timetable.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal", "data", "timetable.json")
        };

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

        // 2. [LEGACY-BRIDGE]: When live browser login is needed on desktop
        return await ExecuteLegacyBridgeAsync(forceLogin, candidateDataPaths, ct);
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

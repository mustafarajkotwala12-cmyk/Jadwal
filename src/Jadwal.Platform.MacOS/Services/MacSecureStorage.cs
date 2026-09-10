using System.Diagnostics;
using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.MacOS.Services;

public class MacSecureStorage : ISecureStorage
{
    private const string ServiceName = "com.jadwal.app";
    private readonly string _fallbackFilePath;

    public MacSecureStorage()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Jadwal");
        Directory.CreateDirectory(appData);
        _fallbackFilePath = Path.Combine(appData, ".secure_store");
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                Arguments = $"find-generic-password -s \"{ServiceName}\" -a \"{key}\" -w",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode == 0)
                {
                    return output.Trim();
                }
            }
        }
        catch
        {
            // Fallback to local protected store
        }

        // Fallback
        if (File.Exists(_fallbackFilePath))
        {
            var lines = await File.ReadAllLinesAsync(_fallbackFilePath, ct);
            foreach (var line in lines)
            {
                var idx = line.IndexOf('=');
                if (idx > 0 && line.Substring(0, idx).Trim() == key)
                {
                    var raw = line.Substring(idx + 1).Trim();
                    try
                    {
                        var bytes = Convert.FromBase64String(raw);
                        return System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        return raw;
                    }
                }
            }
        }

        return null;
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken ct = default)
    {
        var setViaSecurity = false;
        try
        {
            // First delete existing key to avoid duplicates
            await DeleteSecretAsync(key, ct);

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                Arguments = $"add-generic-password -s \"{ServiceName}\" -a \"{key}\" -w \"{value}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode == 0) setViaSecurity = true;
            }
        }
        catch
        {
            // Security CLI unavailable or sandboxed
        }

        if (!setViaSecurity)
        {
            var dict = new Dictionary<string, string>();
            if (File.Exists(_fallbackFilePath))
            {
                var lines = await File.ReadAllLinesAsync(_fallbackFilePath, ct);
                foreach (var line in lines)
                {
                    var idx = line.IndexOf('=');
                    if (idx > 0) dict[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
            dict[key] = b64;
            var newLines = dict.Select(kv => $"{kv.Key}={kv.Value}");
            await File.WriteAllLinesAsync(_fallbackFilePath, newLines, ct);
        }
    }

    public async Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                Arguments = $"delete-generic-password -s \"{ServiceName}\" -a \"{key}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
            }
        }
        catch
        {
            // Ignore
        }

        if (File.Exists(_fallbackFilePath))
        {
            var lines = await File.ReadAllLinesAsync(_fallbackFilePath, ct);
            var remaining = lines.Where(l => !l.StartsWith($"{key}=")).ToList();
            await File.WriteAllLinesAsync(_fallbackFilePath, remaining, ct);
        }
    }
}

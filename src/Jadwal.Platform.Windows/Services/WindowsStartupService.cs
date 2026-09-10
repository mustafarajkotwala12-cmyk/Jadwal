using System.Diagnostics;
using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.Windows.Services;

public class WindowsStartupService : IStartupService
{
    private const string AppName = "Jadwal";

    public async Task<bool> IsLaunchAtStartupEnabledAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"query \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{AppName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    public async Task SetLaunchAtStartupAsync(bool enable, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var processPath = Environment.ProcessPath ?? string.Empty;
            if (enable)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{AppName}\" /t REG_SZ /d \"\\\"{processPath}\\\"\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync(ct);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{AppName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync(ct);
            }
        }
        catch
        {
            // Ignore
        }
    }
}

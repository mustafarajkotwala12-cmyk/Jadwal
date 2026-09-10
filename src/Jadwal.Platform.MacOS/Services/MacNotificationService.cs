using System.Diagnostics;
using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.MacOS.Services;

public class MacNotificationService : INotificationService
{
    public Task<bool> RequestPermissionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public async Task ScheduleNotificationAsync(
        string id,
        string title,
        string body,
        DateTime triggerAt,
        CancellationToken ct = default)
    {
        var delay = triggerAt - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            // For future notifications, trigger after delay in background
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay, ct);
                await DeliverNotificationAsync(title, body);
            }, ct);
        }
        else
        {
            await DeliverNotificationAsync(title, body);
        }
    }

    public Task CancelNotificationAsync(string id, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    private static async Task DeliverNotificationAsync(string title, string body)
    {
        try
        {
            var escapedTitle = title.Replace("\"", "\\\"");
            var escapedBody = body.Replace("\"", "\\\"");
            var script = $"display notification \"{escapedBody}\" with title \"{escapedTitle}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = $"-e '{script}'",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }
        }
        catch
        {
            // Silently ignore notification failure
        }
    }
}

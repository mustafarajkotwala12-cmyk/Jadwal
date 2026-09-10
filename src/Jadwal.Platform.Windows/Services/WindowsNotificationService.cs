using System.Diagnostics;
using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.Windows.Services;

public class WindowsNotificationService : INotificationService
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
            if (OperatingSystem.IsWindows())
            {
                var safeTitle = title.Replace("\"", "`\"");
                var safeBody = body.Replace("\"", "`\"");
                var psScript = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null; " +
                               $"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                               $"$texts = $template.GetElementsByTagName('text'); " +
                               $"$texts[0].AppendChild($template.CreateTextNode('{safeTitle}')) > $null; " +
                               $"$texts[1].AppendChild($template.CreateTextNode('{safeBody}')) > $null; " +
                               $"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " +
                               $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Jadwal').Show($toast);";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }
            }
        }
        catch
        {
            // Silently ignore notification failure
        }
    }
}

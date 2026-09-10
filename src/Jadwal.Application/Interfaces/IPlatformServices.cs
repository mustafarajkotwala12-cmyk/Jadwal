using Jadwal.Domain.Models;

namespace Jadwal.Application.Interfaces;

public interface ISecureStorage
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
    Task SetSecretAsync(string key, string value, CancellationToken ct = default);
    Task DeleteSecretAsync(string key, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<bool> RequestPermissionAsync(CancellationToken ct = default);
    Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt, CancellationToken ct = default);
    Task CancelNotificationAsync(string id, CancellationToken ct = default);
}

public interface IStartupService
{
    Task<bool> IsLaunchAtStartupEnabledAsync(CancellationToken ct = default);
    Task SetLaunchAtStartupAsync(bool enable, CancellationToken ct = default);
}

public interface IJamiaTimetableProvider
{
    Task<bool> HasValidSessionAsync(CancellationToken ct = default);
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<TimetableSnapshot?> FetchCurrentTimetableAsync(bool forceLogin = false, CancellationToken ct = default);
}

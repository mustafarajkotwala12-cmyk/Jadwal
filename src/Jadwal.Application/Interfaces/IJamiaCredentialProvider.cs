namespace Jadwal.Application.Interfaces;

/// <summary>
/// Provider interface decoupling timetable sync components from specific authentication mechanisms.
/// </summary>
public interface IJamiaCredentialProvider
{
    /// <summary>
    /// Obtains a valid Bearer token for Jamia API calls.
    /// Reuses cached token if valid; triggers interactive login if missing or forceRefresh is true.
    /// </summary>
    Task<string?> GetValidTokenAsync(bool forceRefresh = false, CancellationToken ct = default);
}

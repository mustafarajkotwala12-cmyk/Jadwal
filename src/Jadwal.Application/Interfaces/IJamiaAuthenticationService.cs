using Jadwal.Application.Models;

namespace Jadwal.Application.Interfaces;

/// <summary>
/// High-level authentication service for the Jamia Saifiya portal.
/// Handles credential caching, expiration detection, visible browser interactive login,
/// and automated retry on authenticated API failures.
/// </summary>
public interface IJamiaAuthenticationService
{
    /// <summary>
    /// Performs an interactive authentication flow by launching a visible browser.
    /// Closes the browser automatically upon credential capture, cancellation, or error.
    /// Validates the token before persisting to secure storage.
    /// </summary>
    Task<AuthSession> AuthenticateInteractiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Attempts to retrieve a valid, unexpired session from secure storage without launching a browser.
    /// Returns null if credentials are missing, expired, or invalid.
    /// </summary>
    Task<AuthSession?> TryGetValidSessionAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates and removes the stored session token (e.g. after receiving 401/403).
    /// Preserves user ID/password configuration if present.
    /// </summary>
    Task InvalidateAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears all Jamia credentials (token, ITS ID, password) on user logout/disconnect.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current authentication connection status.
    /// </summary>
    Task<JamiaAuthStatus> GetStatusAsync(CancellationToken ct = default);
}

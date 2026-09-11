namespace Jadwal.Application.Models;

public enum JamiaAuthStatus
{
    CheckingConnection,
    Connected,
    AuthenticationRequired,
    Reconnecting,
    Error
}

/// <summary>
/// Immutable representation of an authenticated Jamia portal session.
/// Contains only the minimum required session material and expiry metadata.
/// Never exposes plaintext passwords or cookies.
/// </summary>
public record AuthSession(
    string AccessToken,
    string? ItsId,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyDictionary<string, string>? Claims = null)
{
    public bool IsExpired => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTimeOffset.UtcNow.AddSeconds(60);
}

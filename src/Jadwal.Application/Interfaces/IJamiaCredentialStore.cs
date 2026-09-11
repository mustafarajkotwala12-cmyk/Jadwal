namespace Jadwal.Application.Interfaces;

/// <summary>
/// Platform-agnostic contract for storing and retrieving Jamia credentials securely.
/// Backed by platform keychains (macOS Keychain, Windows Credential Manager).
/// </summary>
public interface IJamiaCredentialStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task SetAccessTokenAsync(string token, CancellationToken ct = default);
    Task<string?> GetItsIdAsync(CancellationToken ct = default);
    Task SetItsIdAsync(string itsId, CancellationToken ct = default);
    Task<string?> GetItsPasswordAsync(CancellationToken ct = default);
    Task SetItsPasswordAsync(string password, CancellationToken ct = default);
    Task ClearCredentialsAsync(CancellationToken ct = default);
}

using System.Text;
using System.Text.Json;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Models;

namespace Jadwal.Integrations.Jamia.Services;

/// <summary>
/// Core authentication coordinator for Jamia Saifiya portal.
/// Implements credential reuse, interactive Playwright login delegation,
/// proactive expiration checks, and token invalidation.
/// </summary>
public class JamiaAuthenticationService : IJamiaAuthenticationService
{
    private readonly IJamiaCredentialStore _credentialStore;
    private readonly IJamiaInteractiveAuthenticator _authenticator;
    private JamiaAuthStatus _currentStatus = JamiaAuthStatus.CheckingConnection;

    public JamiaAuthenticationService(
        IJamiaCredentialStore credentialStore,
        IJamiaInteractiveAuthenticator authenticator)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    public async Task<AuthSession?> TryGetValidSessionAsync(CancellationToken ct = default)
    {
        var token = await _credentialStore.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            _currentStatus = JamiaAuthStatus.AuthenticationRequired;
            return null;
        }

        if (!JwtValidator.IsTokenValid(token))
        {
            // Token is expired or structurally malformed — invalidate
            await InvalidateAsync(ct);
            return null;
        }

        var expectedItsId = await _credentialStore.GetItsIdAsync(ct);
        var (itsId, expiresAt, claims) = ParseJwt(token);

        if (!string.IsNullOrWhiteSpace(expectedItsId) && !string.IsNullOrWhiteSpace(itsId))
        {
            if (!string.Equals(expectedItsId.Trim(), itsId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // Token belongs to a different ITS account — invalidate
                await InvalidateAsync(ct);
                return null;
            }
        }

        _currentStatus = JamiaAuthStatus.Connected;
        return new AuthSession(token, itsId ?? expectedItsId, expiresAt, claims);
    }

    public async Task<AuthSession> AuthenticateInteractiveAsync(CancellationToken ct = default)
    {
        _currentStatus = JamiaAuthStatus.CheckingConnection;

        string? itsId = null;
        string? password = null;

        try
        {
            itsId = await _credentialStore.GetItsIdAsync(ct);
            password = await _credentialStore.GetItsPasswordAsync(ct);
        }
        catch
        {
            // Best effort retrieval for auto-fill
        }

        string rawToken;
        try
        {
            rawToken = await _authenticator.AcquireTokenInteractivelyAsync(itsId, password, ct);
        }
        catch (OperationCanceledException)
        {
            _currentStatus = JamiaAuthStatus.AuthenticationRequired;
            throw;
        }
        catch (Exception)
        {
            _currentStatus = JamiaAuthStatus.Error;
            throw;
        }

        if (string.IsNullOrWhiteSpace(rawToken) || !JwtValidator.IsTokenValid(rawToken))
        {
            _currentStatus = JamiaAuthStatus.Error;
            throw new InvalidOperationException("Interactive authentication did not yield a valid session token.");
        }

        var (tokenItsId, expiresAt, claims) = ParseJwt(rawToken);

        // Persist token to secure storage
        await _credentialStore.SetAccessTokenAsync(rawToken, ct);

        if (!string.IsNullOrWhiteSpace(tokenItsId))
        {
            await _credentialStore.SetItsIdAsync(tokenItsId, ct);
        }

        _currentStatus = JamiaAuthStatus.Connected;
        return new AuthSession(rawToken, tokenItsId ?? itsId, expiresAt, claims);
    }

    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try
        {
            await _credentialStore.SetAccessTokenAsync(string.Empty, ct);
        }
        catch
        {
            // If SetAccessToken fails with empty, fallback to clear or ignore
        }

        _currentStatus = JamiaAuthStatus.AuthenticationRequired;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _credentialStore.ClearCredentialsAsync(ct);
        _currentStatus = JamiaAuthStatus.AuthenticationRequired;
    }

    public async Task<JamiaAuthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var session = await TryGetValidSessionAsync(ct);
        return session != null ? JamiaAuthStatus.Connected : _currentStatus;
    }

    public static (string? itsId, DateTimeOffset? expiresAt, Dictionary<string, string> claims) ParseJwt(string token)
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? itsId = null;
        DateTimeOffset? expiresAt = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, null, claims);
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return (null, null, claims);
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var valStr = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.GetRawText()
                };

                claims[prop.Name] = valStr;

                if (prop.Name.Equals("itsId", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("studentITSID", StringComparison.OrdinalIgnoreCase))
                {
                    itsId = valStr;
                }
                else if (prop.Name.Equals("exp", StringComparison.OrdinalIgnoreCase) &&
                         prop.Value.TryGetInt64(out var expUnix))
                {
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                }
            }
        }
        catch
        {
            // Return whatever was parsed
        }

        return (itsId, expiresAt, claims);
    }
}

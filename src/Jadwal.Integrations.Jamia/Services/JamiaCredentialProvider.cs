using Jadwal.Application.Interfaces;

namespace Jadwal.Integrations.Jamia.Services;

/// <summary>
/// Implements IJamiaCredentialProvider by delegating to IJamiaAuthenticationService.
/// Reuses cached valid tokens before initiating interactive browser login.
/// </summary>
public class JamiaCredentialProvider : IJamiaCredentialProvider
{
    private readonly IJamiaAuthenticationService _authService;

    public JamiaCredentialProvider(IJamiaAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<string?> GetValidTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh)
        {
            var session = await _authService.TryGetValidSessionAsync(ct);
            if (session != null && !session.IsExpired)
            {
                return session.AccessToken;
            }
        }

        var freshSession = await _authService.AuthenticateInteractiveAsync(ct);
        return freshSession.AccessToken;
    }
}

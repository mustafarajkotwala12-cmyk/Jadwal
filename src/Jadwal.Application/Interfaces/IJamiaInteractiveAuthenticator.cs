namespace Jadwal.Application.Interfaces;

/// <summary>
/// Abstraction for the interactive UI login step (e.g. via Microsoft.Playwright).
/// Allows headless/mock substitution for automated testing.
/// </summary>
public interface IJamiaInteractiveAuthenticator
{
    Task<string> AcquireTokenInteractivelyAsync(
        string? targetItsId = null,
        string? targetPassword = null,
        CancellationToken ct = default);
}

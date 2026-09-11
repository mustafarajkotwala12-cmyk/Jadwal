using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Jadwal.Application.Interfaces;
using Microsoft.Playwright;

namespace Jadwal.Integrations.Jamia.Services;

/// <summary>
/// Microsoft.Playwright implementation of interactive login for the Jamia portal.
/// Launches Chromium in headed mode, directs the user through ITS authentication,
/// extracts the JWT session token from network traffic / WebStorage, and validates it.
/// </summary>
public class PlaywrightJamiaAuthenticator : IJamiaInteractiveAuthenticator
{
    private const string JamiaUrl = "https://beta.jameasaifiyah.org/";
    private const string ApiBaseUrl = "https://api.jameasaifiyah.org";
    private static readonly SemaphoreSlim _browserLock = new(1, 1);
    private static bool _driverInstalled;

    public async Task<string> AcquireTokenInteractivelyAsync(
        string? targetItsId = null,
        string? targetPassword = null,
        CancellationToken ct = default)
    {
        // Prevent concurrent interactive browser logins
        await _browserLock.WaitAsync(ct);
        try
        {
            return await ExecuteInteractiveLoginAsync(targetItsId, targetPassword, ct);
        }
        finally
        {
            _browserLock.Release();
        }
    }

    private static async Task<string> ExecuteInteractiveLoginAsync(
        string? targetItsId,
        string? targetPassword,
        CancellationToken ct)
    {
        var profileDir = GetBrowserProfileDirectory();
        Directory.CreateDirectory(profileDir);

        using var playwright = await Playwright.CreateAsync();

        IBrowserContext? context = null;
        var channelsToTry = new[] { null, "chrome", "msedge", "chromium" };
        Exception? lastLaunchErr = null;

        for (int attempt = 0; attempt < 2 && context == null; attempt++)
        {
            foreach (var channel in channelsToTry)
            {
                try
                {
                    var options = new BrowserTypeLaunchPersistentContextOptions
                    {
                        Headless = false,
                        Channel = channel,
                        Args = new[]
                        {
                            "--disable-blink-features=AutomationControlled",
                            "--no-first-run",
                            "--no-default-browser-check"
                        }
                    };

                    context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, options);
                    if (context != null) break;
                }
                catch (Exception ex)
                {
                    lastLaunchErr = ex;
                }
            }

            // If no browser launched on first round, attempt playwright browser install
            if (context == null && attempt == 0 && !_driverInstalled)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                    }, ct);
                    _driverInstalled = true;
                }
                catch
                {
                    // Ignore and proceed to next attempt
                }
            }
        }

        if (context == null)
        {
            throw new InvalidOperationException(
                $"Could not launch any web browser for portal authentication. " +
                $"Please ensure Google Chrome, Microsoft Edge, or Chromium is installed. (Error: {lastLaunchErr?.Message})",
                lastLaunchErr);
        }

        try
        {
            var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

            var capturedTokens = new ConcurrentQueue<string>();

            // Listen for any outgoing response carrying an authorized Bearer JWT
            page.Response += (_, response) =>
            {
                try
                {
                    if (response.Request.Headers.TryGetValue("authorization", out var auth) &&
                        !string.IsNullOrWhiteSpace(auth) &&
                        auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var cand = auth.Substring(7).Trim();
                        if (JwtValidator.IsTokenValid(cand))
                        {
                            capturedTokens.Enqueue(cand);
                        }
                    }
                }
                catch
                {
                    // Ignore response inspection errors
                }
            };

            // Clear any stale session/local storage to ensure a clean login
            try
            {
                await page.GotoAsync(JamiaUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                await page.EvaluateAsync(@"() => {
                    try {
                        sessionStorage.clear();
                        localStorage.clear();
                    } catch (e) {}
                }");
                await page.GotoAsync(JamiaUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            }
            catch
            {
                // Proceed even if initial page load encounters a network redirect
            }

            try
            {
                await page.BringToFrontAsync();
            }
            catch { }

            string? capturedToken = null;
            var maxWaitSeconds = 180;

            for (int i = 0; i < maxWaitSeconds; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (page.IsClosed)
                {
                    throw new OperationCanceledException("Browser window was closed before login completed.");
                }

                // 1. Check network captured tokens
                while (capturedTokens.TryDequeue(out var cand))
                {
                    if (JwtValidator.IsTokenValid(cand))
                    {
                        capturedToken = cand;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(capturedToken)) break;

                // 2. Check web storage
                try
                {
                    var rawToken = await page.EvaluateAsync<string?>(@"() => {
                        try {
                            return sessionStorage.getItem('webauth_token_capture')
                                || sessionStorage.getItem('WEBAUTH_TOKEN_CAPTURE')
                                || localStorage.getItem('access_token')
                                || sessionStorage.getItem('access_token');
                        } catch (e) {
                            return null;
                        }
                    }");

                    if (!string.IsNullOrWhiteSpace(rawToken) && JwtValidator.IsTokenValid(rawToken))
                    {
                        capturedToken = rawToken;
                        break;
                    }
                }
                catch
                {
                    // Page may be navigating; continue
                }

                // 3. Autofill ITS credentials if input fields are present
                if (!string.IsNullOrWhiteSpace(targetItsId))
                {
                    try
                    {
                        var userField = await page.QuerySelectorAsync("input[name='txtUserName'], #txtUserName");
                        if (userField != null)
                        {
                            var currentVal = await userField.InputValueAsync();
                            if (string.IsNullOrEmpty(currentVal))
                            {
                                await userField.FillAsync(targetItsId);
                                if (!string.IsNullOrWhiteSpace(targetPassword))
                                {
                                    var passField = await page.QuerySelectorAsync("input[name='txtPassword'], #txtPassword");
                                    if (passField != null)
                                    {
                                        await passField.FillAsync(targetPassword);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore selector errors while navigating
                    }
                }

                await Task.Delay(1000, ct);
            }

            if (string.IsNullOrEmpty(capturedToken))
            {
                throw new TimeoutException("Authentication timed out after 3 minutes. Please click Sync again and log in.");
            }

            // Immediately validate captured token with a harmless API call
            var isValidWithApi = await ValidateTokenWithApiAsync(capturedToken, ct);
            if (!isValidWithApi)
            {
                // Fallback: if API validation was unreachable but JWT claims and signature are structurally valid
                if (!JwtValidator.IsTokenValid(capturedToken))
                {
                    throw new InvalidOperationException("Captured session token could not be verified by the Jamia Portal.");
                }
            }

            return capturedToken;
        }
        finally
        {
            try
            {
                await context.CloseAsync();
            }
            catch
            {
                // Ignore context close issues
            }
        }
    }

    private static async Task<bool> ValidateTokenWithApiAsync(string token, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Origin", "https://beta.jameasaifiyah.org");
            client.DefaultRequestHeaders.Add("Referer", "https://beta.jameasaifiyah.org/");
            client.DefaultRequestHeaders.Add("X-Menu-Id", "1372");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            int yearAr = 1448;
            string branchId = "3";
            try
            {
                var parts = token.Split('.');
                if (parts.Length == 3)
                {
                    var payload = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("yearAR", out var yProp))
                    {
                        if (yProp.ValueKind == JsonValueKind.Number) yearAr = yProp.GetInt32();
                        else if (int.TryParse(yProp.GetString(), out var yParsed)) yearAr = yParsed;
                    }
                    if (doc.RootElement.TryGetProperty("branchID", out var bProp))
                    {
                        branchId = bProp.ToString();
                    }
                }
            }
            catch { }

            var reqBody = JsonSerializer.Serialize(new { yearAR = yearAr, branchID = branchId, teacherID = "%" });
            var resp = await client.PostAsync(
                $"{ApiBaseUrl}/api/JadwalPage/SelectWeekDDList_JadwalReports",
                new StringContent(reqBody, Encoding.UTF8, "application/json"), ct);

            return resp.IsSuccessStatusCode;
        }
        catch
        {
            // In case of offline/network glitch during verification, return false so caller can check structure
            return false;
        }
    }

    private static string GetBrowserProfileDirectory()
    {
        string baseDir;
        if (OperatingSystem.IsMacOS())
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Jadwal", "data");
        }
        else
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Jadwal", "data");
        }

        return Path.Combine(baseDir, "browser-profile");
    }
}

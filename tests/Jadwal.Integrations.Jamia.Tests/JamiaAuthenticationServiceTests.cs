using System.Text;
using System.Text.Json;
using FluentAssertions;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Models;
using Jadwal.Domain.Models;
using Jadwal.Integrations.Jamia.Services;
using Xunit;

namespace Jadwal.Integrations.Jamia.Tests;

public class JamiaAuthenticationServiceTests
{
    private static string CreateJwt(string itsId, long expUnixSeconds)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadObj = new
        {
            itsId = itsId,
            branchID = 3,
            classID = 3309,
            yearAR = 1448,
            exp = expUnixSeconds
        };
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadObj)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.dummy_signature";
    }

    [Fact]
    public async Task TryGetValidSessionAsync_WithValidUnexpiredToken_ReturnsSessionWithoutCallingAuthenticator()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator();
        var futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var validToken = CreateJwt("50505050", futureExp);

        await store.SetAccessTokenAsync(validToken);
        await store.SetItsIdAsync("50505050");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var session = await authService.TryGetValidSessionAsync();

        session.Should().NotBeNull();
        session!.AccessToken.Should().Be(validToken);
        session.ItsId.Should().Be("50505050");
        session.IsExpired.Should().BeFalse();
        mockAuth.AcquireCallCount.Should().Be(0);

        var status = await authService.GetStatusAsync();
        status.Should().Be(JamiaAuthStatus.Connected);
    }

    [Fact]
    public async Task TryGetValidSessionAsync_WithExpiredToken_InvalidatesAndReturnsNull()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator();
        var pastExp = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
        var expiredToken = CreateJwt("50505050", pastExp);

        await store.SetAccessTokenAsync(expiredToken);
        await store.SetItsIdAsync("50505050");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var session = await authService.TryGetValidSessionAsync();

        session.Should().BeNull();
        var storedToken = await store.GetAccessTokenAsync();
        storedToken.Should().BeNullOrEmpty();
        mockAuth.AcquireCallCount.Should().Be(0);

        var status = await authService.GetStatusAsync();
        status.Should().Be(JamiaAuthStatus.AuthenticationRequired);
    }

    [Fact]
    public async Task TryGetValidSessionAsync_WithDifferentItsId_InvalidatesAndReturnsNull()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator();
        var futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var validTokenForOther = CreateJwt("11111111", futureExp);

        await store.SetAccessTokenAsync(validTokenForOther);
        await store.SetItsIdAsync("22222222"); // Configured for 22222222

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var session = await authService.TryGetValidSessionAsync();

        session.Should().BeNull();
        var storedToken = await store.GetAccessTokenAsync();
        storedToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AuthenticateInteractiveAsync_WhenSuccessful_PersistsTokenAndReturnsSession()
    {
        var store = new MemoryJamiaCredentialStore();
        var futureExp = DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();
        var capturedToken = CreateJwt("70707070", futureExp);
        var mockAuth = new MockInteractiveAuthenticator { TokenToReturn = capturedToken };

        await store.SetItsIdAsync("70707070");
        await store.SetItsPasswordAsync("secret123");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var session = await authService.AuthenticateInteractiveAsync();

        session.Should().NotBeNull();
        session.AccessToken.Should().Be(capturedToken);
        session.ItsId.Should().Be("70707070");

        var savedToken = await store.GetAccessTokenAsync();
        savedToken.Should().Be(capturedToken);

        mockAuth.LastPassedItsId.Should().Be("70707070");
        mockAuth.LastPassedPassword.Should().Be("secret123");

        var status = await authService.GetStatusAsync();
        status.Should().Be(JamiaAuthStatus.Connected);
    }

    [Fact]
    public async Task AuthenticateInteractiveAsync_WhenUserCancels_ThrowsOperationCanceledExceptionWithoutPersisting()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator
        {
            ExceptionToThrow = new OperationCanceledException("User closed browser window.")
        };

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var act = () => authService.AuthenticateInteractiveAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
        var savedToken = await store.GetAccessTokenAsync();
        savedToken.Should().BeNullOrEmpty();

        var status = await authService.GetStatusAsync();
        status.Should().Be(JamiaAuthStatus.AuthenticationRequired);
    }

    [Fact]
    public async Task AuthenticateInteractiveAsync_WhenMalformedTokenCaptured_ThrowsInvalidOperationException()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator { TokenToReturn = "invalid_non_jwt_string" };

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var act = () => authService.AuthenticateInteractiveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        var savedToken = await store.GetAccessTokenAsync();
        savedToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InvalidateAsync_ClearsTokenAndPreservesCredentials()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator();
        var futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var token = CreateJwt("30303030", futureExp);

        await store.SetAccessTokenAsync(token);
        await store.SetItsIdAsync("30303030");
        await store.SetItsPasswordAsync("my_pass");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        await authService.InvalidateAsync();

        var savedToken = await store.GetAccessTokenAsync();
        savedToken.Should().BeNullOrEmpty();

        // ITS ID and Password must remain preserved!
        var itsId = await store.GetItsIdAsync();
        itsId.Should().Be("30303030");
        var pass = await store.GetItsPasswordAsync();
        pass.Should().Be("my_pass");
    }

    [Fact]
    public async Task DisconnectAsync_ClearsAllCredentials()
    {
        var store = new MemoryJamiaCredentialStore();
        var mockAuth = new MockInteractiveAuthenticator();
        var futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var token = CreateJwt("40404040", futureExp);

        await store.SetAccessTokenAsync(token);
        await store.SetItsIdAsync("40404040");
        await store.SetItsPasswordAsync("my_pass");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        await authService.DisconnectAsync();

        var savedToken = await store.GetAccessTokenAsync();
        savedToken.Should().BeNullOrEmpty();

        var itsId = await store.GetItsIdAsync();
        itsId.Should().BeNullOrEmpty();

        var pass = await store.GetItsPasswordAsync();
        pass.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task JamiaCredentialProvider_DelegatesToAuthServiceCorrectly()
    {
        var store = new MemoryJamiaCredentialStore();
        var futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var token1 = CreateJwt("80808080", futureExp);
        var token2 = CreateJwt("80808080", futureExp + 3600);

        var mockAuth = new MockInteractiveAuthenticator { TokenToReturn = token2 };
        await store.SetAccessTokenAsync(token1);
        await store.SetItsIdAsync("80808080");

        var authService = new JamiaAuthenticationService(store, mockAuth);
        var provider = new JamiaCredentialProvider(authService);

        // 1. Normal call should return cached token without calling interactive authenticator
        var tok = await provider.GetValidTokenAsync(forceRefresh: false);
        tok.Should().Be(token1);
        mockAuth.AcquireCallCount.Should().Be(0);

        // 2. Forced refresh call should trigger interactive authenticator
        var freshTok = await provider.GetValidTokenAsync(forceRefresh: true);
        freshTok.Should().Be(token2);
        mockAuth.AcquireCallCount.Should().Be(1);
    }
}

internal class MemoryJamiaCredentialStore : IJamiaCredentialStore
{
    private string? _token;
    private string? _itsId;
    private string? _password;

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult(_token);

    public Task SetAccessTokenAsync(string token, CancellationToken ct = default)
    {
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        return Task.CompletedTask;
    }

    public Task<string?> GetItsIdAsync(CancellationToken ct = default) => Task.FromResult(_itsId);

    public Task SetItsIdAsync(string itsId, CancellationToken ct = default)
    {
        _itsId = string.IsNullOrWhiteSpace(itsId) ? null : itsId;
        return Task.CompletedTask;
    }

    public Task<string?> GetItsPasswordAsync(CancellationToken ct = default) => Task.FromResult(_password);

    public Task SetItsPasswordAsync(string password, CancellationToken ct = default)
    {
        _password = string.IsNullOrWhiteSpace(password) ? null : password;
        return Task.CompletedTask;
    }

    public Task ClearCredentialsAsync(CancellationToken ct = default)
    {
        _token = null;
        _itsId = null;
        _password = null;
        return Task.CompletedTask;
    }
}

internal class MockInteractiveAuthenticator : IJamiaInteractiveAuthenticator
{
    public string? TokenToReturn { get; set; }
    public Exception? ExceptionToThrow { get; set; }
    public int AcquireCallCount { get; private set; }
    public string? LastPassedItsId { get; private set; }
    public string? LastPassedPassword { get; private set; }

    public Task<string> AcquireTokenInteractivelyAsync(
        string? targetItsId = null,
        string? targetPassword = null,
        CancellationToken ct = default)
    {
        AcquireCallCount++;
        LastPassedItsId = targetItsId;
        LastPassedPassword = targetPassword;

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(TokenToReturn ?? "");
    }
}

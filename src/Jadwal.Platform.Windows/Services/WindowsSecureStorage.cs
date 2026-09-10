using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.Windows.Services;

public class WindowsSecureStorage : ISecureStorage
{
    private readonly string _storageFilePath;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Jadwal.Entropy.v2");

    public WindowsSecureStorage()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jadwal");
        Directory.CreateDirectory(appData);
        _storageFilePath = Path.Combine(appData, "secure_credentials.bin");
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        if (!File.Exists(_storageFilePath)) return null;

        try
        {
            var dict = await ReadStoreAsync(ct);
            return dict.TryGetValue(key, out var val) ? val : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken ct = default)
    {
        var dict = await ReadStoreAsync(ct);
        dict[key] = value;
        await WriteStoreAsync(dict, ct);
    }

    public async Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        var dict = await ReadStoreAsync(ct);
        if (dict.Remove(key))
        {
            await WriteStoreAsync(dict, ct);
        }
    }

    private async Task<Dictionary<string, string>> ReadStoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_storageFilePath)) return new Dictionary<string, string>();

        var bytes = await File.ReadAllBytesAsync(_storageFilePath, ct);
        if (bytes.Length == 0) return new Dictionary<string, string>();

        byte[] plainBytes;
        if (OperatingSystem.IsWindows())
        {
            plainBytes = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            // Cross-platform fallback for testing/dev
            plainBytes = bytes;
        }

        var json = Encoding.UTF8.GetString(plainBytes);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private async Task WriteStoreAsync(Dictionary<string, string> dict, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(dict);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        byte[] encryptedBytes;
        if (OperatingSystem.IsWindows())
        {
            encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            encryptedBytes = plainBytes;
        }

        await File.WriteAllBytesAsync(_storageFilePath, encryptedBytes, ct);
    }
}

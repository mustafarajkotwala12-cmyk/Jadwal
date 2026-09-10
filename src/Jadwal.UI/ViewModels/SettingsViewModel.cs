using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISecureStorage _secureStorage;
    private readonly TimetableService _timetableService;
    private readonly ILegacyMigrationService _legacyMigrator;

    [ObservableProperty]
    private string _itsId = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isCredentialsSaved = false;

    [ObservableProperty]
    private string _storedItsId = string.Empty;

    [ObservableProperty]
    private string _storedCredentialStatusText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isSuccess = false;

    [ObservableProperty]
    private string _migrationReport = string.Empty;

    public Func<Task<string?>>? PickFileHandler { get; set; }

    public SettingsViewModel(
        ISecureStorage secureStorage,
        TimetableService timetableService,
        ILegacyMigrationService legacyMigrator)
    {
        _secureStorage = secureStorage;
        _timetableService = timetableService;
        _legacyMigrator = legacyMigrator;
    }

    public async Task InitializeAsync()
    {
        var savedId = await _secureStorage.GetSecretAsync("its_id") ?? string.Empty;
        var savedPass = await _secureStorage.GetSecretAsync("its_password") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(savedId))
        {
            ItsId = savedId;
            StoredItsId = savedId;
            IsCredentialsSaved = true;
            StoredCredentialStatusText = OperatingSystem.IsWindows()
                ? "Credentials Encrypted & Secured in Windows DPAPI Vault"
                : OperatingSystem.IsMacOS()
                    ? "Credentials Encrypted & Secured in Apple Keychain"
                    : "Credentials Encrypted & Secured in OS Vault";
        }
        else
        {
            IsCredentialsSaved = false;
            StoredItsId = string.Empty;
            StoredCredentialStatusText = "No credentials currently saved in OS vault";
        }

        if (!string.IsNullOrWhiteSpace(savedPass))
        {
            Password = savedPass;
        }
    }

    [RelayCommand]
    public async Task SaveCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(ItsId))
        {
            StatusMessage = "Please enter your ITS ID";
            IsSuccess = false;
            return;
        }

        await _secureStorage.SetSecretAsync("its_id", ItsId.Trim());
        if (!string.IsNullOrEmpty(Password))
        {
            await _secureStorage.SetSecretAsync("its_password", Password);
        }

        StoredItsId = ItsId.Trim();
        IsCredentialsSaved = true;
        StoredCredentialStatusText = OperatingSystem.IsWindows()
            ? "Credentials Encrypted & Secured in Windows DPAPI Vault"
            : OperatingSystem.IsMacOS()
                ? "Credentials Encrypted & Secured in Apple Keychain"
                : "Credentials Encrypted & Secured in OS Vault";

        StatusMessage = $"Credentials for ITS {StoredItsId} saved and encrypted in OS vault.";
        IsSuccess = true;
    }

    [RelayCommand]
    public async Task ClearCredentialsAsync()
    {
        await _secureStorage.DeleteSecretAsync("its_id");
        await _secureStorage.DeleteSecretAsync("its_password");
        await _secureStorage.DeleteSecretAsync("jamea_access_token");

        ItsId = string.Empty;
        Password = string.Empty;
        StoredItsId = string.Empty;
        IsCredentialsSaved = false;
        StoredCredentialStatusText = "No credentials currently saved in OS vault";

        StatusMessage = "Stored credentials removed from OS vault.";
        IsSuccess = true;
    }

    [RelayCommand]
    public async Task SyncJamiaScheduleAsync()
    {
        IsBusy = true;
        StatusMessage = "Syncing timetable from Jamia Portal...";
        IsSuccess = false;

        try
        {
            await SaveCredentialsAsync();
            var changes = await _timetableService.RefreshTimetableAsync(forceLogin: true);
            StatusMessage = $"Sync successful! Detected {changes.Count} schedule changes.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync notice: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ImportTimetableFileAsync(string? explicitFilePath = null)
    {
        var filePath = explicitFilePath;
        if (string.IsNullOrEmpty(filePath) && PickFileHandler != null)
        {
            filePath = await PickFileHandler();
        }

        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Importing timetable from {Path.GetFileName(filePath)}...";
        IsSuccess = false;

        try
        {
            TimetableSnapshot snapshot;
            if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                snapshot = ExcelTimetableParser.Parse(filePath);
            }
            else
            {
                var json = await File.ReadAllTextAsync(filePath);
                snapshot = TimetableJsonParser.ParseJson(json);
            }

            var count = await _timetableService.SaveImportedSnapshotAsync(snapshot);
            StatusMessage = $"Timetable imported successfully! Loaded {count} class periods from {Path.GetFileName(filePath)}.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RunLegacyMigrationAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking for legacy JameaHelper / Jadwal data...";
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var legacyStoreDir = Path.Combine(home, "JameaHelper", "Data");
            if (!Directory.Exists(legacyStoreDir))
            {
                legacyStoreDir = Path.Combine(home, "Library", "Application Support", "JameaHelper");
            }

            var result = await _legacyMigrator.MigrateAllAsync(legacyStoreDir);
            if (result.Success)
            {
                MigrationReport = $"Migration complete: {result.TasksMigrated} tasks migrated. Timetable migrated: {result.TimetableMigrated}. Backup: {result.BackupPath}";
                StatusMessage = "Legacy migration completed successfully!";
                IsSuccess = true;
            }
            else
            {
                StatusMessage = $"Migration notice: {result.ErrorMessage ?? "No legacy files found to migrate."}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Migration error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

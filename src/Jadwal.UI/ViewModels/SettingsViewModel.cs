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
    private readonly ThemeService _themeService;
    private readonly TodayViewModel? _todayVm;
    private readonly IJamiaCredentialStore? _credentialStore;

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

    // Theme properties
    [ObservableProperty]
    private string _selectedTheme = "Light";

    [ObservableProperty]
    private bool _isLightTheme = true;

    [ObservableProperty]
    private bool _isDarkTheme = false;

    // Credits & About Information
    public string AppName => "Jadwal (جدول)";
    public string DeveloperName => "Mustafa Rajkotwala";
    public string AppVersion => "4.1.0 (LTS)";
    public string AppSubtitle => "Academic Schedule & Task Companion for Aljamea-tus-Saifiyah";

    // Sync Interval (bound to TodayViewModel)
    public int[] SyncIntervalOptions => TodayViewModel.SyncIntervalOptions;

    public int SyncIntervalMinutes
    {
        get => _todayVm?.SyncIntervalMinutes ?? 4;
        set
        {
            if (_todayVm != null)
            {
                _todayVm.SyncIntervalMinutes = value;
                OnPropertyChanged();
            }
        }
    }

    public Func<Task<string?>>? PickFileHandler { get; set; }

    public SettingsViewModel(
        ISecureStorage secureStorage,
        TimetableService timetableService,
        ILegacyMigrationService legacyMigrator,
        ThemeService? themeService = null,
        TodayViewModel? todayVm = null,
        IJamiaCredentialStore? credentialStore = null)
    {
        _secureStorage = secureStorage;
        _timetableService = timetableService;
        _legacyMigrator = legacyMigrator;
        _themeService = themeService ?? new ThemeService();
        _todayVm = todayVm;
        _credentialStore = credentialStore;

        var savedTheme = _themeService.GetSavedTheme();
        SelectedTheme = savedTheme switch
        {
            JadwalThemeMode.Dark => "Dark",
            _ => "Light"
        };
        UpdateThemeState(SelectedTheme);
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
            StoredCredentialStatusText = "No credentials currently saved in OS vault";
        }

        if (!string.IsNullOrEmpty(savedPass))
        {
            Password = savedPass;
        }

        ApplyTheme(SelectedTheme);
    }

    [RelayCommand]
    public void SetTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return;
        SelectedTheme = themeName;
        UpdateThemeState(themeName);
        ApplyTheme(themeName);

        var mode = themeName == "Dark" ? JadwalThemeMode.Dark : JadwalThemeMode.Light;
        _themeService.SaveTheme(mode);
    }

    private void UpdateThemeState(string theme)
    {
        IsLightTheme = theme == "Light";
        IsDarkTheme = theme == "Dark";
    }

    private void ApplyTheme(string theme)
    {
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = theme switch
            {
                "Dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Light
            };
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

        var trimmedId = ItsId.Trim();
        var oldId = await _secureStorage.GetSecretAsync("its_id");
        if (!string.IsNullOrEmpty(oldId) && !string.Equals(oldId, trimmedId, StringComparison.OrdinalIgnoreCase))
        {
            // ITS ID changed! Invalidate any previous session token for the old user
            await _secureStorage.DeleteSecretAsync("jamea_access_token");
        }

        await _secureStorage.SetSecretAsync("its_id", trimmedId);
        if (!string.IsNullOrEmpty(Password))
        {
            await _secureStorage.SetSecretAsync("its_password", Password);
        }

        StoredItsId = trimmedId;
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

        if (_credentialStore != null)
        {
            await _credentialStore.ClearCredentialsAsync();
        }

        ItsId = string.Empty;
        Password = string.Empty;
        StoredItsId = string.Empty;
        IsCredentialsSaved = false;
        StoredCredentialStatusText = "No credentials currently saved in OS vault";

        StatusMessage = "Stored credentials and session removed from OS vault.";
        IsSuccess = true;
    }

    [RelayCommand]
    public async Task ResetAllAppDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Purging all local credentials, caches, and app data...";
        IsSuccess = false;

        try
        {
            // 1. Clear credentials from secure storage and credential store
            await _secureStorage.DeleteSecretAsync("its_id");
            await _secureStorage.DeleteSecretAsync("its_password");
            await _secureStorage.DeleteSecretAsync("jamea_access_token");

            if (_credentialStore != null)
            {
                await _credentialStore.ClearCredentialsAsync();
            }

            // 2. Wipe data files from user Application Support / AppData
            var rootDir = OperatingSystem.IsMacOS()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Jadwal")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal");

            if (Directory.Exists(rootDir))
            {
                var filesToDelete = new[]
                {
                    "stored_timetable.json",
                    "stored_tasks.json",
                    "stored_tasks.json.bak",
                    "stored_changes.json",
                    ".secure_store",
                    "secure_credentials.bin",
                    Path.Combine("data", "jamea_token.json"),
                    Path.Combine("data", "timetable.json")
                };

                foreach (var file in filesToDelete)
                {
                    var fullPath = Path.Combine(rootDir, file);
                    try
                    {
                        if (File.Exists(fullPath)) File.Delete(fullPath);
                    }
                    catch { /* best effort */ }
                }

                var browserProfileDir = Path.Combine(rootDir, "data", "browser-profile");
                try
                {
                    if (Directory.Exists(browserProfileDir))
                    {
                        Directory.Delete(browserProfileDir, recursive: true);
                    }
                }
                catch { /* best effort */ }
            }

            // 3. Reset in-memory state
            ItsId = string.Empty;
            Password = string.Empty;
            StoredItsId = string.Empty;
            IsCredentialsSaved = false;
            StoredCredentialStatusText = "No credentials currently saved in OS vault";

            if (_todayVm != null)
            {
                await _todayVm.InitializeAsync();
            }

            StatusMessage = "All application data and OS vault credentials have been completely reset.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset encountered an issue: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SyncJamiaScheduleAsync()
    {
        await PerformSyncAsync(forceLogin: false);
    }

    [RelayCommand]
    public async Task ReauthenticatePortalAsync()
    {
        await PerformSyncAsync(forceLogin: true);
    }

    private async Task PerformSyncAsync(bool forceLogin)
    {
        IsBusy = true;
        IsSuccess = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(ItsId))
            {
                await SaveCredentialsAsync();
            }

            IReadOnlyList<TimetableChangeRecord> changes;

            if (forceLogin)
            {
                StatusMessage = "Launching portal login window. Please authenticate in the browser...";
                changes = await _timetableService.RefreshTimetableAsync(forceLogin: true);
            }
            else
            {
                var hasValidSession = await _timetableService.HasValidSessionAsync();
                if (!hasValidSession)
                {
                    StatusMessage = "Session expired or authentication required. Opening login window...";
                    changes = await _timetableService.RefreshTimetableAsync(forceLogin: true);
                }
                else
                {
                    StatusMessage = "Synchronizing directly via secure portal connection...";
                    try
                    {
                        changes = await _timetableService.RefreshTimetableAsync(forceLogin: false);
                    }
                    catch (Exception)
                    {
                        // Direct fetch failed (e.g. server returned 401 or token was invalidated)
                        StatusMessage = "Portal session expired. Opening login window to re-authenticate...";
                        changes = await _timetableService.RefreshTimetableAsync(forceLogin: true);
                    }
                }
            }

            var idLabel = !string.IsNullOrEmpty(StoredItsId) ? $" for ITS {StoredItsId}" : "";
            StatusMessage = changes.Count > 0
                ? $"Sync successful! Updated timetable with {changes.Count} changes{idLabel}."
                : $"Sync complete! Your timetable is up to date (0 changes detected{idLabel}).";
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
        StatusMessage = $"Importing timetable from {System.IO.Path.GetFileName(filePath)}...";
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
            StatusMessage = $"Timetable imported successfully! Loaded {count} class periods from {System.IO.Path.GetFileName(filePath)}.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to import timetable file: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RunLegacyMigrationCommand()
    {
        IsBusy = true;
        StatusMessage = "Scanning and migrating legacy data...";
        IsSuccess = false;
        MigrationReport = string.Empty;

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
                MigrationReport = $"Migration complete: {result.TasksMigrated} tasks migrated. Timetable: {(result.TimetableMigrated ? "imported" : "unchanged")}. Backup: {result.BackupPath}";
                StatusMessage = "Legacy data migration successful!";
                IsSuccess = true;
            }
            else
            {
                MigrationReport = $"Migration notice: {result.ErrorMessage ?? "No legacy files found to migrate."}";
                StatusMessage = "Legacy data migration completed.";
                IsSuccess = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Migration error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

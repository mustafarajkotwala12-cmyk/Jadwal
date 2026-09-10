using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;

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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isSuccess = false;

    [ObservableProperty]
    private string _migrationReport = string.Empty;

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
        ItsId = await _secureStorage.GetSecretAsync("its_id") ?? string.Empty;
        Password = await _secureStorage.GetSecretAsync("its_password") ?? string.Empty;
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

        StatusMessage = "Credentials saved securely.";
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
            StatusMessage = $"Sync successful! Detected {changes.Count} new schedule changes.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync error: {ex.Message}";
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

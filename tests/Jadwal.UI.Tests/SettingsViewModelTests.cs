using FluentAssertions;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Models;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;
using Jadwal.UI.ViewModels;
using Xunit;

namespace Jadwal.UI.Tests;

public class MemorySecureStorage : ISecureStorage
{
    public Dictionary<string, string> Storage { get; } = new();

    public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        Storage.TryGetValue(key, out var val);
        return Task.FromResult<string?>(val);
    }

    public Task SetSecretAsync(string key, string value, CancellationToken ct = default)
    {
        Storage[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        Storage.Remove(key);
        return Task.CompletedTask;
    }
}

public class DummyLegacyMigrator : ILegacyMigrationService
{
    public Task<MigrationResultDto> MigrateAllAsync(string legacySourceDirectory, CancellationToken ct = default)
    {
        return Task.FromResult(new MigrationResultDto(true, legacySourceDirectory, "", 0, 0, false, null, null));
    }
}

public class SettingsViewModelTests
{
    [Fact]
    public async Task SettingsViewModel_Initialize_WithSavedCredentials_ShowsSavedStatus()
    {
        var secureStorage = new MemorySecureStorage();
        await secureStorage.SetSecretAsync("its_id", "10000001");
        await secureStorage.SetSecretAsync("its_password", "SecretPass123");

        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);
        await vm.InitializeAsync();

        vm.IsCredentialsSaved.Should().BeTrue();
        vm.StoredItsId.Should().Be("10000001");
        vm.ItsId.Should().Be("10000001");
        vm.Password.Should().Be("SecretPass123");
        vm.StoredCredentialStatusText.Should().Contain("Secured");
    }

    [Fact]
    public async Task SettingsViewModel_SaveCredentials_UpdatesSavedStatusAndStorage()
    {
        var secureStorage = new MemorySecureStorage();
        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);
        vm.ItsId = "30399999";
        vm.Password = "NewPass456";

        await vm.SaveCredentialsAsync();

        vm.IsCredentialsSaved.Should().BeTrue();
        vm.StoredItsId.Should().Be("30399999");
        secureStorage.Storage.Should().ContainKey("its_id").WhoseValue.Should().Be("30399999");
        secureStorage.Storage.Should().ContainKey("its_password").WhoseValue.Should().Be("NewPass456");
        vm.StatusMessage.Should().Contain("30399999");
    }

    [Fact]
    public async Task SettingsViewModel_ClearCredentials_RemovesSecretsAndResetsStatus()
    {
        var secureStorage = new MemorySecureStorage();
        await secureStorage.SetSecretAsync("its_id", "10000001");
        await secureStorage.SetSecretAsync("its_password", "SecretPass123");

        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);
        await vm.InitializeAsync();
        vm.IsCredentialsSaved.Should().BeTrue();

        await vm.ClearCredentialsAsync();

        vm.IsCredentialsSaved.Should().BeFalse();
        vm.StoredItsId.Should().BeEmpty();
        vm.ItsId.Should().BeEmpty();
        vm.Password.Should().BeEmpty();
        secureStorage.Storage.Should().NotContainKey("its_id");
        secureStorage.Storage.Should().NotContainKey("its_password");
    }

    [Fact]
    public async Task SettingsViewModel_ImportTimetableFile_SavesSnapshotToRepository()
    {
        var secureStorage = new MemorySecureStorage();
        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);

        var tempJson = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempJson, @"{
                ""academicYear"": ""1447 / 1448"",
                ""weekNumber"": 25,
                ""entries"": [
                    {
                        ""day"": ""Monday"",
                        ""period"": ""Period 1"",
                        ""startTime"": ""07:00"",
                        ""endTime"": ""07:45"",
                        ""subject"": ""Fiqh"",
                        ""details"": ""Room 101""
                    }
                ]
            }");

            await vm.ImportTimetableFileAsync(tempJson);

            vm.IsSuccess.Should().BeTrue();
            vm.StatusMessage.Should().Contain("Loaded 1 class periods");

            var saved = await timetableRepo.GetLatestSnapshotAsync();
            saved.Should().NotBeNull();
            saved!.Periods.Should().HaveCount(1);
            saved.Periods[0].Subject.Should().Be("Fiqh");
        }
        finally
        {
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
    }

    [Fact]
    public void SettingsViewModel_Credits_HasMustafaRajkotwalaAndAppName()
    {
        var secureStorage = new MemorySecureStorage();
        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);

        vm.AppName.Should().Contain("Jadwal");
        vm.DeveloperName.Should().Be("Mustafa Rajkotwala");
        vm.AppVersion.Should().Contain("4.1.0");
    }

    [Fact]
    public void SettingsViewModel_SetTheme_UpdatesThemeAndState()
    {
        var secureStorage = new MemorySecureStorage();
        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var themeService = new ThemeService(tempDir);

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator, themeService);

        vm.SetTheme("Dark");
        vm.SelectedTheme.Should().Be("Dark");
        vm.IsDarkTheme.Should().BeTrue();
        vm.IsLightTheme.Should().BeFalse();

        // Check persistence
        themeService.GetSavedTheme().Should().Be(JadwalThemeMode.Dark);

        vm.SetTheme("Light");
        vm.SelectedTheme.Should().Be("Light");
        vm.IsLightTheme.Should().BeTrue();
        vm.IsDarkTheme.Should().BeFalse();
        themeService.GetSavedTheme().Should().Be(JadwalThemeMode.Light);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task SettingsViewModel_ResetAllAppData_WipesCredentialsAndResetsStatus()
    {
        var secureStorage = new MemorySecureStorage();
        await secureStorage.SetSecretAsync("its_id", "10000001");
        await secureStorage.SetSecretAsync("its_password", "SecretPass123");
        await secureStorage.SetSecretAsync("jamea_access_token", "test-token");

        var timetableRepo = new MemoryTimetableRepo();
        var changeRepo = new MemoryChangeRepo();
        var taskRepo = new MemoryTaskRepo();
        var dummyProvider = new DummyJamiaProvider();

        var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
        var migrator = new DummyLegacyMigrator();

        var vm = new SettingsViewModel(secureStorage, timetableService, migrator);
        await vm.InitializeAsync();
        vm.IsCredentialsSaved.Should().BeTrue();

        await vm.ResetAllAppDataAsync();

        vm.IsCredentialsSaved.Should().BeFalse();
        vm.StoredItsId.Should().BeEmpty();
        vm.ItsId.Should().BeEmpty();
        vm.Password.Should().BeEmpty();
        secureStorage.Storage.Should().NotContainKey("its_id");
        secureStorage.Storage.Should().NotContainKey("its_password");
        secureStorage.Storage.Should().NotContainKey("jamea_access_token");
        vm.StatusMessage.Should().Contain("reset");
    }

    [Fact]
    public void SettingsViewModel_SetFontSize_UpdatesSelectionAndPersistence()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jadwal_test_font_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var themeService = new ThemeService(tempDir);
            var secureStorage = new MemorySecureStorage();
            var timetableRepo = new MemoryTimetableRepo();
            var changeRepo = new MemoryChangeRepo();
            var taskRepo = new MemoryTaskRepo();
            var dummyProvider = new DummyJamiaProvider();
            var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
            var migrator = new DummyLegacyMigrator();

            var vm = new SettingsViewModel(secureStorage, timetableService, migrator, themeService);

            vm.SelectedFontSize.Should().Be("Normal");
            vm.IsNormalFontSize.Should().BeTrue();
            vm.IsLargeFontSize.Should().BeFalse();
            vm.IsExtraLargeFontSize.Should().BeFalse();

            vm.SetFontSize("Large");
            vm.SelectedFontSize.Should().Be("Large");
            vm.IsNormalFontSize.Should().BeFalse();
            vm.IsLargeFontSize.Should().BeTrue();
            vm.IsExtraLargeFontSize.Should().BeFalse();
            themeService.GetSavedFontSize().Should().Be("Large");
            themeService.GetSavedFontScale().Should().Be(1.15);

            vm.SetFontSize("ExtraLarge");
            vm.SelectedFontSize.Should().Be("ExtraLarge");
            vm.IsExtraLargeFontSize.Should().BeTrue();
            themeService.GetSavedFontSize().Should().Be("ExtraLarge");
            themeService.GetSavedFontScale().Should().Be(1.30);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SettingsViewModel_DisableSync_UpdatesStateAndTodayViewModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jadwal_test_sync_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var themeService = new ThemeService(tempDir);
            var secureStorage = new MemorySecureStorage();
            var timetableRepo = new MemoryTimetableRepo();
            var changeRepo = new MemoryChangeRepo();
            var taskRepo = new MemoryTaskRepo();
            var dummyProvider = new DummyJamiaProvider();
            var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
            var migrator = new DummyLegacyMigrator();
            var timeProvider = new TestTimeProvider();
            var taskService = new TaskService(taskRepo, timeProvider);
            var dashboardService = new DashboardService(timetableRepo, taskRepo, changeRepo, timeProvider);
            var todayVm = new TodayViewModel(dashboardService, taskService, timetableService, timeProvider, themeService: themeService);

            var vm = new SettingsViewModel(secureStorage, timetableService, migrator, themeService, todayVm);

            vm.IsSyncDisabled.Should().BeFalse();
            todayVm.IsSyncDisabled.Should().BeFalse();

            vm.IsSyncDisabled = true;
            vm.IsSyncDisabled.Should().BeTrue();
            todayVm.IsSyncDisabled.Should().BeTrue();
            themeService.GetSavedSyncDisabled().Should().BeTrue();

            vm.IsSyncDisabled = false;
            vm.IsSyncDisabled.Should().BeFalse();
            todayVm.IsSyncDisabled.Should().BeFalse();
            themeService.GetSavedSyncDisabled().Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BreakBarViewModel_RefreshmentBar_IsThinnerThanNamazBar()
    {
        var recessInfo = new BreakBarInfo(
            Id: "break_recess",
            Name: "Morning Recess Break",
            NamaazNote: "Refreshment & Academic Preparation",
            StartTime: "10:35",
            EndTime: "10:55",
            DurationMinutes: 20,
            Icon: "☕"
        );

        var namazInfo = new BreakBarInfo(
            Id: "break_lunch_namaz",
            Name: "Lunch & Namaz Break",
            NamaazNote: "🕌 Zawaal 12:21 PM  ·  Zuhr End 2:15 PM",
            StartTime: "12:40",
            EndTime: "14:00",
            DurationMinutes: 80,
            Icon: "☀️",
            ZawaalTime: "12:21 PM",
            ZuhrEndTime: "2:15 PM"
        );

        var recessVm = new BreakBarViewModel(recessInfo);
        var namazVm = new BreakBarViewModel(namazInfo);

        // Refreshment bar must be flagged correctly
        recessVm.IsRefreshmentBreak.Should().BeTrue();
        recessVm.IsNamazBreak.Should().BeFalse();

        // Namaz bar must be flagged correctly
        namazVm.IsRefreshmentBreak.Should().BeFalse();
        namazVm.IsNamazBreak.Should().BeTrue();
        namazVm.HasNamaazTimings.Should().BeTrue();

        // Refreshment bar must have significantly thinner padding, margin, and typography
        recessVm.BarPadding.Top.Should().BeLessThan(namazVm.BarPadding.Top);
        recessVm.BarPadding.Bottom.Should().BeLessThan(namazVm.BarPadding.Bottom);
        recessVm.BarMargin.Top.Should().BeLessThan(namazVm.BarMargin.Top);
        recessVm.IconFontSize.Should().BeLessThan(namazVm.IconFontSize);
        recessVm.TitleFontSize.Should().BeLessThan(namazVm.TitleFontSize);
    }

    [Fact]
    public async Task SettingsViewModel_CitySelection_DefaultsToMumbaiAndPersistsChange()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jadwal_test_city_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var prayerService = new PrayerTimeService(dataDirectory: tempDir);
            var themeService = new ThemeService(tempDir);
            var secureStorage = new MemorySecureStorage();
            var timetableRepo = new MemoryTimetableRepo();
            var changeRepo = new MemoryChangeRepo();
            var taskRepo = new MemoryTaskRepo();
            var dummyProvider = new DummyJamiaProvider();
            var timetableService = new TimetableService(timetableRepo, changeRepo, taskRepo, dummyProvider);
            var migrator = new DummyLegacyMigrator();

            var vm = new SettingsViewModel(
                secureStorage,
                timetableService,
                migrator,
                themeService: themeService,
                prayerTimeService: prayerService);

            // Default city must be Mumbai (116)
            vm.SelectedCity.Should().NotBeNull();
            vm.SelectedCity.Id.Should().Be(116);
            vm.SelectedCity.DisplayName.Should().Be("Mumbai");
            vm.AvailableCities.Should().NotBeEmpty();
            vm.AvailableCities.First().DisplayName.Should().Be("Mumbai");

            // Change city to Surat (145)
            var surat = vm.AvailableCities.First(c => c.DisplayName == "Surat");
            vm.SelectedCity = surat;

            // Persisted in PrayerTimeService
            prayerService.GetSavedCity().Id.Should().Be(145);
            prayerService.GetSavedCity().DisplayName.Should().Be("Surat");

            // Preview update is triggered
            await vm.UpdateNamazPreviewAsync();
            vm.NamazTimingsPreview.Should().Contain("Zawaal");
            vm.NamazTimingsPreview.Should().Contain("Zuhr End");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

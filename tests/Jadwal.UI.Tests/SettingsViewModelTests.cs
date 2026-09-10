using FluentAssertions;
using Jadwal.Application.Interfaces;
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
        await secureStorage.SetSecretAsync("its_id", "30327222");
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
        vm.StoredItsId.Should().Be("30327222");
        vm.ItsId.Should().Be("30327222");
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
        await secureStorage.SetSecretAsync("its_id", "30327222");
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
}

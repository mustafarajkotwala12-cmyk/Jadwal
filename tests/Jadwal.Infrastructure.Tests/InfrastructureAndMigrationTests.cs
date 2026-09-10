using System.Text.Json;
using FluentAssertions;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Jadwal.Infrastructure.Migration;
using Jadwal.Infrastructure.Persistence;
using Xunit;

namespace Jadwal.Infrastructure.Tests;

public class InfrastructureAndMigrationTests
{
    [Fact]
    public async Task JsonFileTaskRepository_PersistsAndLoadsTasks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "jadwal_test_" + Guid.NewGuid());
        try
        {
            var repo = new JsonFileTaskRepository(tempDir);
            var task = new TaskItem("Memorize Juz Amma", "Review with mentor", TaskPriority.High, TaskCategory.Hifz);
            await repo.SaveTaskAsync(task);

            var all = await repo.GetAllTasksAsync();
            all.Should().HaveCount(1);
            all.First().Title.Should().Be("Memorize Juz Amma");
            all.First().Category.Should().Be(TaskCategory.Hifz);

            // Delete
            await repo.DeleteTaskAsync(task.Id);
            var empty = await repo.GetAllTasksAsync();
            empty.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LegacyDataMigrator_CreatesBackupAndPreservesAllFields()
    {
        var legacyDir = Path.Combine(Path.GetTempPath(), "legacy_test_" + Guid.NewGuid());
        var targetDir = Path.Combine(Path.GetTempPath(), "target_test_" + Guid.NewGuid());

        try
        {
            Directory.CreateDirectory(legacyDir);

            // Copy fixture sample tasks
            var fixtureTasks = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "tools", "fixtures", "sample_tasks.json"));
            File.WriteAllText(Path.Combine(legacyDir, "stored_tasks.json"), fixtureTasks);

            var migrator = new LegacyDataMigrator();
            var report = await migrator.MigrateAsync(legacyDir, targetDir);

            report.Success.Should().BeTrue();
            report.TasksMigratedCount.Should().Be(3);
            report.BackupDirectory.Should().NotBeNull();
            Directory.Exists(report.BackupDirectory!).Should().BeTrue();

            // Verify target tasks repository can load migrated data
            var targetRepo = new JsonFileTaskRepository(targetDir);
            var tasks = await targetRepo.GetAllTasksAsync();
            tasks.Should().HaveCount(3);
            tasks.Should().Contain(t => t.Title.Contains("Surah Al-Naba"));
            tasks.Should().Contain(t => t.LinkedSubject == "الرسالة الشريفة");

            // Verify schema version written
            File.Exists(Path.Combine(targetDir, "schema_version.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(legacyDir)) Directory.Delete(legacyDir, true);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task JsonFileTimetableRepository_EmptyStorage_SeedsFromEmbeddedOrBundledTimetable()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "timetable_seed_test_" + Guid.NewGuid());
        try
        {
            var repo = new JsonFileTimetableRepository(emptyDir);
            var snapshot = await repo.GetLatestSnapshotAsync();

            snapshot.Should().NotBeNull();
            snapshot!.Periods.Should().NotBeEmpty();

            // Verify it was persisted to stored_timetable.json
            File.Exists(Path.Combine(emptyDir, "stored_timetable.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(emptyDir)) Directory.Delete(emptyDir, true);
        }
    }
}

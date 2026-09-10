using System.Text.Json;
using Jadwal.Domain.Models;

using Jadwal.Application.Interfaces;

namespace Jadwal.Infrastructure.Migration;

public record MigrationReport(
    bool Success,
    string SourceDirectory,
    string TargetDirectory,
    int TasksMigratedCount,
    int ChangesMigratedCount,
    bool TimetableMigrated,
    string? BackupDirectory,
    IReadOnlyList<string> Warnings
);

public class LegacyDataMigrator : ILegacyMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _defaultTargetDirectory;

    public LegacyDataMigrator(string? defaultTargetDirectory = null)
    {
        _defaultTargetDirectory = defaultTargetDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jadwal");
    }

    public async Task<MigrationResultDto> MigrateAllAsync(string legacySourceDirectory, CancellationToken ct = default)
    {
        var report = await MigrateAsync(legacySourceDirectory, _defaultTargetDirectory, ct);
        return new MigrationResultDto(
            Success: report.Success,
            SourceDirectory: report.SourceDirectory,
            TargetDirectory: report.TargetDirectory,
            TasksMigrated: report.TasksMigratedCount,
            ChangesMigrated: report.ChangesMigratedCount,
            TimetableMigrated: report.TimetableMigrated,
            BackupPath: report.BackupDirectory,
            ErrorMessage: report.Warnings.Count > 0 ? string.Join("; ", report.Warnings) : null
        );
    }

    public async Task<MigrationReport> MigrateAsync(
        string legacyDirectory,
        string targetDirectory,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();
        if (!Directory.Exists(legacyDirectory))
        {
            return new MigrationReport(false, legacyDirectory, targetDirectory, 0, 0, false, null, new[] { "Legacy directory does not exist." });
        }

        Directory.CreateDirectory(targetDirectory);

        // Step 1: Create Backup
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(targetDirectory, "backups", $"backup_{timestamp}");
        Directory.CreateDirectory(backupDir);

        foreach (var file in Directory.GetFiles(legacyDirectory, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(backupDir, fileName), overwrite: true);
        }

        // Step 2: Migrate Tasks
        var tasksMigrated = 0;
        var legacyTasksFile = Path.Combine(legacyDirectory, "stored_tasks.json");
        if (File.Exists(legacyTasksFile))
        {
            try
            {
                await using var stream = File.OpenRead(legacyTasksFile);
                var tasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, JsonOptions, ct);
                if (tasks != null)
                {
                    var targetTasksFile = Path.Combine(targetDirectory, "stored_tasks.json");
                    await using var writeStream = File.Create(targetTasksFile);
                    await JsonSerializer.SerializeAsync(writeStream, tasks, JsonOptions, ct);
                    tasksMigrated = tasks.Count;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse tasks: {ex.Message}");
            }
        }

        // Step 3: Migrate Changes
        var changesMigrated = 0;
        var legacyChangesFile = Path.Combine(legacyDirectory, "stored_changes.json");
        if (File.Exists(legacyChangesFile))
        {
            try
            {
                await using var stream = File.OpenRead(legacyChangesFile);
                var changes = await JsonSerializer.DeserializeAsync<List<TimetableChangeRecord>>(stream, JsonOptions, ct);
                if (changes != null)
                {
                    var targetChangesFile = Path.Combine(targetDirectory, "stored_changes.json");
                    await using var writeStream = File.Create(targetChangesFile);
                    await JsonSerializer.SerializeAsync(writeStream, changes, JsonOptions, ct);
                    changesMigrated = changes.Count;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse changes: {ex.Message}");
            }
        }

        // Step 4: Migrate Timetable
        var timetableMigrated = false;
        var legacyTimetableFile = Path.Combine(legacyDirectory, "stored_timetable.json");
        if (!File.Exists(legacyTimetableFile))
        {
            // Fallback to Data/timetable.json if stored_timetable doesn't exist
            var rootTimetable = Path.Combine(legacyDirectory, "timetable.json");
            if (File.Exists(rootTimetable)) legacyTimetableFile = rootTimetable;
        }

        if (File.Exists(legacyTimetableFile))
        {
            try
            {
                await using var stream = File.OpenRead(legacyTimetableFile);
                var snapshot = await JsonSerializer.DeserializeAsync<TimetableSnapshot>(stream, JsonOptions, ct);
                if (snapshot != null)
                {
                    var targetTimetableFile = Path.Combine(targetDirectory, "stored_timetable.json");
                    await using var writeStream = File.Create(targetTimetableFile);
                    await JsonSerializer.SerializeAsync(writeStream, snapshot, JsonOptions, ct);
                    timetableMigrated = true;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse timetable: {ex.Message}");
            }
        }

        // Step 5: Save Schema Version
        var versionInfo = new
        {
            SchemaVersion = 2,
            MigratedAt = DateTime.UtcNow,
            Source = legacyDirectory,
            TasksCount = tasksMigrated,
            ChangesCount = changesMigrated,
            TimetableMigrated = timetableMigrated
        };
        var versionFile = Path.Combine(targetDirectory, "schema_version.json");
        await File.WriteAllTextAsync(versionFile, JsonSerializer.Serialize(versionInfo, JsonOptions), ct);

        return new MigrationReport(
            Success: true,
            SourceDirectory: legacyDirectory,
            TargetDirectory: targetDirectory,
            TasksMigratedCount: tasksMigrated,
            ChangesMigratedCount: changesMigrated,
            TimetableMigrated: timetableMigrated,
            BackupDirectory: backupDir,
            Warnings: warnings
        );
    }
}

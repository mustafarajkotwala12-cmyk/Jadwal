namespace Jadwal.Application.Interfaces;

public record MigrationResultDto(
    bool Success,
    string SourceDirectory,
    string TargetDirectory,
    int TasksMigrated,
    int ChangesMigrated,
    bool TimetableMigrated,
    string? BackupPath,
    string? ErrorMessage
);

public interface ILegacyMigrationService
{
    Task<MigrationResultDto> MigrateAllAsync(string legacySourceDirectory, CancellationToken ct = default);
}

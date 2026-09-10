using System.Text.Json;
using Jadwal.Application.Interfaces;
using Jadwal.Domain.Models;

namespace Jadwal.Infrastructure.Persistence;

public class JsonFileTaskRepository : ITaskRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string GetDefaultStorageDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jadwal");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public JsonFileTaskRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? GetDefaultStorageDirectory();
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "stored_tasks.json");
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTasksAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<TaskItem>();
            await using var stream = File.OpenRead(_filePath);
            var tasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, JsonOptions, ct);
            return (tasks ?? new List<TaskItem>()).AsReadOnly();
        }
        catch (JsonException)
        {
            // Corrupt file detection - fallback to backup if available
            var backupPath = _filePath + ".bak";
            if (File.Exists(backupPath))
            {
                await using var stream = File.OpenRead(backupPath);
                var tasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, JsonOptions, ct);
                return (tasks ?? new List<TaskItem>()).AsReadOnly();
            }
            return Array.Empty<TaskItem>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await GetAllTasksAsync(ct);
        return all.FirstOrDefault(t => t.Id == id);
    }

    public async Task SaveTaskAsync(TaskItem task, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var tasks = new List<TaskItem>();
            if (File.Exists(_filePath))
            {
                try
                {
                    await using var readStream = File.OpenRead(_filePath);
                    tasks = (await JsonSerializer.DeserializeAsync<List<TaskItem>>(readStream, JsonOptions, ct)) ?? new List<TaskItem>();
                }
                catch
                {
                    tasks = new List<TaskItem>();
                }
            }

            var index = tasks.FindIndex(t => t.Id == task.Id);
            if (index >= 0)
                tasks[index] = task;
            else
                tasks.Add(task);

            await SafeWriteAtomicAsync(_filePath, tasks, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return;
            List<TaskItem> tasks;
            await using (var readStream = File.OpenRead(_filePath))
            {
                tasks = (await JsonSerializer.DeserializeAsync<List<TaskItem>>(readStream, JsonOptions, ct)) ?? new List<TaskItem>();
            }

            tasks.RemoveAll(t => t.Id == id);
            await SafeWriteAtomicAsync(_filePath, tasks, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task SafeWriteAtomicAsync<T>(string targetPath, T data, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        var backupPath = targetPath + ".bak";

        await using (var tempStream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(tempStream, data, JsonOptions, ct);
        }

        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, backupPath, overwrite: true);
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }
}

public class JsonFileTimetableRepository : ITimetableRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonFileTimetableRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "stored_timetable.json");
    }

    public async Task<TimetableSnapshot?> GetLatestSnapshotAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return null;
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<TimetableSnapshot>(stream, JsonOptions, ct);
        }
        catch
        {
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSnapshotAsync(TimetableSnapshot snapshot, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public class JsonFileChangeRepository : IChangeRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonFileChangeRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "stored_changes.json");
    }

    public async Task<IReadOnlyList<TimetableChangeRecord>> GetChangesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<TimetableChangeRecord>();
            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync<List<TimetableChangeRecord>>(stream, JsonOptions, ct);
            return (list ?? new List<TimetableChangeRecord>()).AsReadOnly();
        }
        catch
        {
            return Array.Empty<TimetableChangeRecord>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AppendChangesAsync(IEnumerable<TimetableChangeRecord> changes, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = new List<TimetableChangeRecord>();
            if (File.Exists(_filePath))
            {
                try
                {
                    await using var readStream = File.OpenRead(_filePath);
                    list = (await JsonSerializer.DeserializeAsync<List<TimetableChangeRecord>>(readStream, JsonOptions, ct)) ?? new List<TimetableChangeRecord>();
                }
                catch { }
            }

            list.AddRange(changes);

            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AcknowledgeChangeAsync(Guid changeId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return;
            List<TimetableChangeRecord> list;
            await using (var readStream = File.OpenRead(_filePath))
            {
                list = (await JsonSerializer.DeserializeAsync<List<TimetableChangeRecord>>(readStream, JsonOptions, ct)) ?? new List<TimetableChangeRecord>();
            }

            var item = list.FirstOrDefault(c => c.Id == changeId);
            if (item != null)
            {
                item.IsAcknowledged = true;
                var tempPath = _filePath + ".tmp";
                await using (var writeStream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
                }
                File.Move(tempPath, _filePath, overwrite: true);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AcknowledgeAllChangesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return;
            List<TimetableChangeRecord> list;
            await using (var readStream = File.OpenRead(_filePath))
            {
                list = (await JsonSerializer.DeserializeAsync<List<TimetableChangeRecord>>(readStream, JsonOptions, ct)) ?? new List<TimetableChangeRecord>();
            }

            foreach (var item in list) item.IsAcknowledged = true;

            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public class JsonFileProgramRepository : IProgramRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonFileProgramRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "stored_programs.json");
    }

    public async Task<IReadOnlyList<ProgramItem>> GetAllProgramsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<ProgramItem>();
            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<List<ProgramItem>>(stream, JsonOptions, ct);
            return (items ?? new List<ProgramItem>()).AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveProgramAsync(ProgramItem program, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = new List<ProgramItem>();
            if (File.Exists(_filePath))
            {
                await using var readStream = File.OpenRead(_filePath);
                list = (await JsonSerializer.DeserializeAsync<List<ProgramItem>>(readStream, JsonOptions, ct)) ?? new List<ProgramItem>();
            }

            list.RemoveAll(p => p.Id == program.Id);
            list.Add(program);

            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteProgramAsync(Guid id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return;
            List<ProgramItem> list;
            await using (var readStream = File.OpenRead(_filePath))
            {
                list = (await JsonSerializer.DeserializeAsync<List<ProgramItem>>(readStream, JsonOptions, ct)) ?? new List<ProgramItem>();
            }

            list.RemoveAll(p => p.Id == id);
            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public class JsonFileNoteRepository : INoteRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonFileNoteRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "stored_notes.json");
    }

    public async Task<IReadOnlyList<NoteItem>> GetAllNotesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<NoteItem>();
            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<List<NoteItem>>(stream, JsonOptions, ct);
            return (items ?? new List<NoteItem>()).AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveNoteAsync(NoteItem note, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = new List<NoteItem>();
            if (File.Exists(_filePath))
            {
                await using var readStream = File.OpenRead(_filePath);
                list = (await JsonSerializer.DeserializeAsync<List<NoteItem>>(readStream, JsonOptions, ct)) ?? new List<NoteItem>();
            }

            list.RemoveAll(n => n.Id == note.Id);
            list.Add(note);

            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return;
            List<NoteItem> list;
            await using (var readStream = File.OpenRead(_filePath))
            {
                list = (await JsonSerializer.DeserializeAsync<List<NoteItem>>(readStream, JsonOptions, ct)) ?? new List<NoteItem>();
            }

            list.RemoveAll(n => n.Id == id);
            var tempPath = _filePath + ".tmp";
            await using (var writeStream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(writeStream, list, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}

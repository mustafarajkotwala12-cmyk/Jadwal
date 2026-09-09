import Foundation
import SwiftCore

public actor LocalFileTimetableRepository: TimetableRepository {
    private let fileURL: URL
    private let fileManager = FileManager.default

    public init(storageDirectory: URL) {
        self.fileURL = storageDirectory.appendingPathComponent("stored_timetable.json")
        try? fileManager.createDirectory(at: storageDirectory, withIntermediateDirectories: true)
    }

    public func loadLatestSnapshot() async throws -> TimetableSnapshot? {
        guard fileManager.fileExists(atPath: fileURL.path) else {
            return nil
        }
        let data = try Data(contentsOf: fileURL)
        return try JSONDecoder().decode(TimetableSnapshot.self, from: data)
    }

    public func saveSnapshot(_ snapshot: TimetableSnapshot) async throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(snapshot)
        try data.write(to: fileURL, options: .atomic)
    }
}

public actor LocalFileTaskRepository: TaskRepository {
    private let fileURL: URL
    private let fileManager = FileManager.default

    public init(storageDirectory: URL) {
        self.fileURL = storageDirectory.appendingPathComponent("stored_tasks.json")
        try? fileManager.createDirectory(at: storageDirectory, withIntermediateDirectories: true)
    }

    public func fetchTasks() async throws -> [TaskItem] {
        guard fileManager.fileExists(atPath: fileURL.path) else {
            return []
        }
        let data = try Data(contentsOf: fileURL)
        return try JSONDecoder().decode([TaskItem].self, from: data)
    }

    public func saveTask(_ task: TaskItem) async throws {
        var tasks: [TaskItem] = []
        if fileManager.fileExists(atPath: fileURL.path) {
            let data = try Data(contentsOf: fileURL)
            tasks = (try? JSONDecoder().decode([TaskItem].self, from: data)) ?? []
        }

        if let index = tasks.firstIndex(where: { $0.id == task.id }) {
            tasks[index] = task
        } else {
            tasks.append(task)
        }

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(tasks)
        try data.write(to: fileURL, options: .atomic)
    }

    public func deleteTask(id: UUID) async throws {
        guard fileManager.fileExists(atPath: fileURL.path) else { return }
        let data = try Data(contentsOf: fileURL)
        var tasks = (try? JSONDecoder().decode([TaskItem].self, from: data)) ?? []
        tasks.removeAll(where: { $0.id == id })

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let dataToSave = try encoder.encode(tasks)
        try dataToSave.write(to: fileURL, options: .atomic)
    }
}

public actor LocalFileChangeRepository {
    private let fileURL: URL
    private let fileManager = FileManager.default

    public init(storageDirectory: URL) {
        self.fileURL = storageDirectory.appendingPathComponent("stored_changes.json")
        try? fileManager.createDirectory(at: storageDirectory, withIntermediateDirectories: true)
    }

    public func fetchChanges() async throws -> [TimetableChangeRecord] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        let data = try Data(contentsOf: fileURL)
        return try JSONDecoder().decode([TimetableChangeRecord].self, from: data)
    }

    public func appendChanges(_ newChanges: [TimetableChangeRecord]) async throws {
        var existing = (try? await fetchChanges()) ?? []
        existing.append(contentsOf: newChanges)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(existing)
        try data.write(to: fileURL, options: .atomic)
    }

    public func acknowledgeChange(id: UUID) async throws {
        var changes = (try? await fetchChanges()) ?? []
        if let idx = changes.firstIndex(where: { $0.id == id }) {
            changes[idx].isAcknowledged = true
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            let data = try encoder.encode(changes)
            try data.write(to: fileURL, options: .atomic)
        }
    }

    public func acknowledgeAllChanges() async throws {
        var changes = (try? await fetchChanges()) ?? []
        for i in 0..<changes.count {
            changes[i].isAcknowledged = true
        }
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(changes)
        try data.write(to: fileURL, options: .atomic)
    }

    public func clearAllChanges() async throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode([TimetableChangeRecord]())
        try data.write(to: fileURL, options: .atomic)
    }
}

import Foundation

public final class TaskService: Sendable {
    private let repository: TaskRepository

    public init(repository: TaskRepository) {
        self.repository = repository
    }

    public func getAllTasks() async throws -> [TaskItem] {
        try await repository.fetchTasks()
    }

    public func createTask(
        title: String,
        notes: String? = nil,
        priority: TaskPriority = .medium,
        dueDate: Date? = nil,
        linkedSubject: String? = nil,
        linkedPeriodId: String? = nil
    ) async throws -> TaskItem {
        let task = TaskItem(
            title: title,
            notes: notes,
            priority: priority,
            dueDate: dueDate,
            linkedSubject: linkedSubject,
            linkedPeriodId: linkedPeriodId
        )
        try await repository.saveTask(task)
        return task
    }

    public func toggleTaskCompletion(task: TaskItem) async throws -> TaskItem {
        var updated = task
        if updated.isCompleted {
            updated.reopen()
        } else {
            updated.markCompleted()
        }
        try await repository.saveTask(updated)
        return updated
    }

    public func updateTask(_ task: TaskItem) async throws {
        try await repository.saveTask(task)
    }

    public func deleteTask(id: UUID) async throws {
        try await repository.deleteTask(id: id)
    }
}

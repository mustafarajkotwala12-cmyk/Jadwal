import Foundation

public protocol TimetableRepository: Sendable {
    func loadLatestSnapshot() async throws -> TimetableSnapshot?
    func saveSnapshot(_ snapshot: TimetableSnapshot) async throws
}

public protocol TaskRepository: Sendable {
    func fetchTasks() async throws -> [TaskItem]
    func saveTask(_ task: TaskItem) async throws
    func deleteTask(id: UUID) async throws
}

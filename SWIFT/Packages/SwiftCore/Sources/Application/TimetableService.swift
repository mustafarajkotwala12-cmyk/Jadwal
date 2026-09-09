import Foundation

public final class TimetableService: Sendable {
    private let repository: TimetableRepository

    public init(repository: TimetableRepository) {
        self.repository = repository
    }

    public func getLatestTimetable() async throws -> TimetableSnapshot? {
        try await repository.loadLatestSnapshot()
    }

    public func saveTimetable(_ snapshot: TimetableSnapshot) async throws {
        try await repository.saveSnapshot(snapshot)
    }
}

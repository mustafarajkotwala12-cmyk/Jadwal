import Foundation
import JadwalCore

public struct RawTimetableEntry: Codable, Sendable {
    public let day: String
    public let date: String?
    public let period: String
    public let startTime: String
    public let endTime: String
    public let subject: String
    public let details: String?
}

public struct RawTimetablePayload: Codable, Sendable {
    public let academicYear: String
    public let weekNumber: Int?
    public let startDate: String?
    public let endDate: String?
    public let entries: [RawTimetableEntry]
}

public final class TimetableImporter: Sendable {
    public init() {}

    public func importFromJSONData(_ data: Data) throws -> TimetableSnapshot {
        let raw = try JSONDecoder().decode(RawTimetablePayload.self, from: data)
        return normalize(rawPayload: raw)
    }

    public func importFromFile(at url: URL) throws -> TimetableSnapshot {
        let data = try Data(contentsOf: url)
        return try importFromJSONData(data)
    }

    private func normalize(rawPayload: RawTimetablePayload) -> TimetableSnapshot {
        let occurrences: [PeriodOccurrence] = rawPayload.entries.compactMap { entry in
            guard let dayEnum = DayOfWeek(rawValue: entry.day) else {
                return nil
            }
            return PeriodOccurrence(
                day: dayEnum,
                dateString: entry.date,
                periodName: entry.period,
                startTime: entry.startTime,
                endTime: entry.endTime,
                subject: entry.subject,
                details: entry.details ?? ""
            )
        }

        return TimetableSnapshot(
            academicYear: rawPayload.academicYear,
            weekNumber: rawPayload.weekNumber,
            startDate: rawPayload.startDate,
            endDate: rawPayload.endDate,
            periods: occurrences
        )
    }
}

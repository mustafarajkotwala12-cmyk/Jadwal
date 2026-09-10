import Foundation

public enum DayOfWeek: String, Codable, CaseIterable, Sendable {
    case monday = "Monday"
    case tuesday = "Tuesday"
    case wednesday = "Wednesday"
    case thursday = "Thursday"
    case friday = "Friday"
    case saturday = "Saturday"
    case sunday = "Sunday"

    public var arabicName: String {
        switch self {
        case .monday: return "يوم الاثنين"
        case .tuesday: return "يوم الثلاثاء"
        case .wednesday: return "يوم الاربعاء"
        case .thursday: return "يوم الخميس"
        case .friday: return "يوم الجمعة"
        case .saturday: return "يوم السبت"
        case .sunday: return "يوم الأحد"
        }
    }
}

public enum TimetableChangeType: String, Codable, Sendable {
    case added = "ADDED"
    case removed = "REMOVED"
    case subjectChanged = "SUBJECT_CHANGED"
    case timeChanged = "TIME_CHANGED"
    case detailsChanged = "DETAILS_CHANGED"
}

public struct TimetableChangeRecord: Identifiable, Codable, Hashable, Sendable {
    public let id: UUID
    public let periodId: String
    public let day: DayOfWeek
    public let dateString: String?
    public let periodName: String
    public let changeType: TimetableChangeType
    public let oldSubject: String?
    public let newSubject: String?
    public let oldStartTime: String?
    public let newStartTime: String?
    public let oldEndTime: String?
    public let newEndTime: String?
    public let oldDetails: String?
    public let newDetails: String?
    public let timestamp: Date
    public var isAcknowledged: Bool

    public init(
        id: UUID = UUID(),
        periodId: String,
        day: DayOfWeek,
        dateString: String?,
        periodName: String,
        changeType: TimetableChangeType,
        oldSubject: String? = nil,
        newSubject: String? = nil,
        oldStartTime: String? = nil,
        newStartTime: String? = nil,
        oldEndTime: String? = nil,
        newEndTime: String? = nil,
        oldDetails: String? = nil,
        newDetails: String? = nil,
        timestamp: Date = Date(),
        isAcknowledged: Bool = false
    ) {
        self.id = id
        self.periodId = periodId
        self.day = day
        self.dateString = dateString
        self.periodName = periodName
        self.changeType = changeType
        self.oldSubject = oldSubject
        self.newSubject = newSubject
        self.oldStartTime = oldStartTime
        self.newStartTime = newStartTime
        self.oldEndTime = oldEndTime
        self.newEndTime = newEndTime
        self.oldDetails = oldDetails
        self.newDetails = newDetails
        self.timestamp = timestamp
        self.isAcknowledged = isAcknowledged
    }

    public var summaryMessage: String {
        switch changeType {
        case .subjectChanged:
            return "\(periodName): Changed from \(oldSubject ?? "Previous") to \(newSubject ?? "New")"
        case .timeChanged:
            return "\(periodName) (\(newSubject ?? "")): Time shifted to \(newStartTime ?? "")–\(newEndTime ?? "")"
        case .added:
            return "\(periodName): Added \(newSubject ?? "") (\(newStartTime ?? "")–\(newEndTime ?? ""))"
        case .removed:
            return "\(periodName): \(oldSubject ?? "") removed from timetable"
        case .detailsChanged:
            return "\(periodName): Details updated for \(newSubject ?? "")"
        }
    }
}

public struct PeriodOccurrence: Identifiable, Codable, Hashable, Sendable {
    public let id: String
    public let day: DayOfWeek
    public let dateString: String?
    public let periodName: String
    public var startTime: String
    public var endTime: String
    public var subject: String
    public var details: String
    public var changeRecord: TimetableChangeRecord?

    public init(
        id: String? = nil,
        day: DayOfWeek,
        dateString: String?,
        periodName: String,
        startTime: String,
        endTime: String,
        subject: String,
        details: String,
        changeRecord: TimetableChangeRecord? = nil
    ) {
        self.day = day
        self.dateString = dateString
        self.periodName = periodName
        self.startTime = startTime
        self.endTime = endTime
        self.subject = subject
        self.details = details
        self.changeRecord = changeRecord

        // Stable slot ID based on date/day and period slot name
        if let id = id {
            self.id = id
        } else {
            let datePart = dateString ?? day.rawValue
            self.id = "\(datePart)_\(periodName)".replacingOccurrences(of: " ", with: "_")
        }
    }
}

public struct TimetableSnapshot: Codable, Sendable {
    public let academicYear: String
    public let weekNumber: Int?
    public let startDate: String?
    public let endDate: String?
    public var periods: [PeriodOccurrence]

    public init(
        academicYear: String,
        weekNumber: Int? = nil,
        startDate: String? = nil,
        endDate: String? = nil,
        periods: [PeriodOccurrence]
    ) {
        self.academicYear = academicYear
        self.weekNumber = weekNumber
        self.startDate = startDate
        self.endDate = endDate
        self.periods = periods
    }

    public func periods(for day: DayOfWeek) -> [PeriodOccurrence] {
        periods.filter { $0.day == day }
    }

    public func periods(forDate dateStr: String) -> [PeriodOccurrence] {
        periods.filter { $0.dateString == dateStr }
    }

    public var uniqueSubjects: [String] {
        Array(Set(periods.map(\.subject))).sorted()
    }
}

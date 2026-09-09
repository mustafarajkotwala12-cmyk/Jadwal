import Foundation

public enum TaskPriority: String, Codable, CaseIterable, Comparable, Sendable {
    case low = "Low"
    case medium = "Medium"
    case high = "High"
    case urgent = "Urgent"

    private var sortOrder: Int {
        switch self {
        case .low: return 0
        case .medium: return 1
        case .high: return 2
        case .urgent: return 3
        }
    }

    public static func < (lhs: TaskPriority, rhs: TaskPriority) -> Bool {
        lhs.sortOrder < rhs.sortOrder
    }
}

public enum TaskStatus: String, Codable, Sendable {
    case pending = "Pending"
    case completed = "Completed"
    case archived = "Archived"
}

public struct TaskDiscrepancy: Codable, Hashable, Sendable {
    public let changeRecordId: UUID
    public let changeType: TimetableChangeType
    public let message: String
    public let oldContext: String
    public let newContext: String
    public let periodId: String?
    public var isDismissed: Bool

    public var originalSubject: String { oldContext }
    public var newSubject: String { newContext }

    public init(
        changeRecordId: UUID = UUID(),
        changeType: TimetableChangeType = .subjectChanged,
        message: String = "",
        oldContext: String,
        newContext: String,
        periodId: String? = nil,
        isDismissed: Bool = false
    ) {
        self.changeRecordId = changeRecordId
        self.changeType = changeType
        self.message = message.isEmpty ? "Slot subject changed from \(oldContext) to \(newContext)" : message
        self.oldContext = oldContext
        self.newContext = newContext
        self.periodId = periodId
        self.isDismissed = isDismissed
    }

    public init(
        originalSubject: String,
        newSubject: String,
        periodId: String? = nil,
        changeType: TimetableChangeType = .subjectChanged,
        isDismissed: Bool = false
    ) {
        self.changeRecordId = UUID()
        self.changeType = changeType
        self.message = "Slot subject changed from \(originalSubject) to \(newSubject)"
        self.oldContext = originalSubject
        self.newContext = newSubject
        self.periodId = periodId
        self.isDismissed = isDismissed
    }
}

public struct TaskItem: Identifiable, Codable, Hashable, Sendable {
    public let id: UUID
    public var title: String
    public var notes: String?
    public var priority: TaskPriority
    public var status: TaskStatus
    public var dueDate: Date?
    public var linkedSubject: String?
    public var linkedPeriodId: String?
    public let createdAt: Date
    public var completedAt: Date?
    public var discrepancy: TaskDiscrepancy?

    public init(
        id: UUID = UUID(),
        title: String,
        notes: String? = nil,
        priority: TaskPriority = .medium,
        status: TaskStatus = .pending,
        dueDate: Date? = nil,
        linkedSubject: String? = nil,
        linkedPeriodId: String? = nil,
        createdAt: Date = Date(),
        completedAt: Date? = nil,
        discrepancy: TaskDiscrepancy? = nil
    ) {
        self.id = id
        self.title = title
        self.notes = notes
        self.priority = priority
        self.status = status
        self.dueDate = dueDate
        self.linkedSubject = linkedSubject
        self.linkedPeriodId = linkedPeriodId
        self.createdAt = createdAt
        self.completedAt = completedAt
        self.discrepancy = discrepancy
    }

    public var isCompleted: Bool {
        status == .completed
    }

    public mutating func markCompleted() {
        status = .completed
        completedAt = Date()
    }

    public mutating func reopen() {
        status = .pending
        completedAt = nil
    }

    public mutating func dismissDiscrepancy() {
        self.discrepancy?.isDismissed = true
    }

    public mutating func adoptNewSubject(_ newSubject: String? = nil) {
        if let explicit = newSubject {
            self.linkedSubject = explicit
        } else if let newCtx = self.discrepancy?.newContext {
            self.linkedSubject = newCtx
        }
        self.discrepancy = nil
    }

    public mutating func keepOriginalContext() {
        self.discrepancy?.isDismissed = true
    }
}

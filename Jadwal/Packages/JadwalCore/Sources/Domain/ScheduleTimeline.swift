import Foundation

public enum ClassLiveStatus: Equatable, Sendable {
    case completed
    case inProgress
    case startingSoon(minutes: Int)
    case upcoming
    case cancelled
    case changed(summary: String)

    public var isEmphasized: Bool {
        switch self {
        case .inProgress, .startingSoon:
            return true
        default:
            return false
        }
    }

    public static func compute(for period: PeriodOccurrence, currentTime: Date = Date()) -> ClassLiveStatus {
        if let change = period.changeRecord, !change.isAcknowledged {
            if change.changeType == .removed {
                return .cancelled
            }
            return .changed(summary: change.summaryMessage)
        }

        let calendar = Calendar.current
        let currentHour = calendar.component(.hour, from: currentTime)
        let currentMinute = calendar.component(.minute, from: currentTime)
        let currentMinutes = currentHour * 60 + currentMinute

        guard let startMinutes = parseMinutes(from: period.startTime),
              let endMinutes = parseMinutes(from: period.endTime) else {
            return .upcoming
        }

        if currentMinutes > endMinutes {
            return .completed
        } else if currentMinutes >= startMinutes && currentMinutes <= endMinutes {
            return .inProgress
        } else {
            let diff = startMinutes - currentMinutes
            if diff > 0 && diff <= 15 {
                return .startingSoon(minutes: diff)
            } else {
                return .upcoming
            }
        }
    }

    private static func parseMinutes(from timeStr: String) -> Int? {
        let parts = timeStr.trimmingCharacters(in: .whitespaces).split(separator: ":")
        guard parts.count == 2,
              let h = Int(parts[0]),
              let m = Int(parts[1]) else {
            return nil
        }
        return h * 60 + m
    }
}

public enum ScheduleTimelineItem: Identifiable, Sendable {
    case classPeriod(period: PeriodOccurrence, status: ClassLiveStatus)
    case breakBlock(id: String, name: String, startTime: String, endTime: String, durationMinutes: Int)

    public var id: String {
        switch self {
        case .classPeriod(let period, _):
            return period.id
        case .breakBlock(let id, _, _, _, _):
            return id
        }
    }

    public var startTime: String {
        switch self {
        case .classPeriod(let period, _):
            return period.startTime
        case .breakBlock(_, _, let start, _, _):
            return start
        }
    }

    public var endTime: String {
        switch self {
        case .classPeriod(let period, _):
            return period.endTime
        case .breakBlock(_, _, _, let end, _):
            return end
        }
    }
}

public enum ScheduleTimelineBuilder {
    public static func parseMinutes(from timeStr: String) -> Int? {
        let parts = timeStr.trimmingCharacters(in: .whitespaces).split(separator: ":")
        guard parts.count == 2,
              let h = Int(parts[0]),
              let m = Int(parts[1]) else {
            return nil
        }
        return h * 60 + m
    }

    public static func formatDuration(minutes: Int) -> String {
        if minutes >= 60 {
            let hours = minutes / 60
            let remainder = minutes % 60
            if remainder == 0 {
                return "\(hours)h"
            }
            return "\(hours)h \(remainder)m"
        }
        return "\(minutes)m"
    }

    public static func buildTimeline(from periods: [PeriodOccurrence], currentTime: Date = Date()) -> [ScheduleTimelineItem] {
        guard !periods.isEmpty else { return [] }

        // Sort chronologically by startTime
        let sorted = periods.sorted { (p1, p2) -> Bool in
            let m1 = parseMinutes(from: p1.startTime) ?? 0
            let m2 = parseMinutes(from: p2.startTime) ?? 0
            return m1 < m2
        }

        var items: [ScheduleTimelineItem] = []

        for i in 0..<sorted.count {
            let current = sorted[i]
            let status = ClassLiveStatus.compute(for: current, currentTime: currentTime)
            items.append(.classPeriod(period: current, status: status))

            // Check if there is a gap to next period
            if i + 1 < sorted.count {
                let next = sorted[i + 1]
                if let endMin = parseMinutes(from: current.endTime),
                   let nextStartMin = parseMinutes(from: next.startTime) {
                    let gap = nextStartMin - endMin
                    if gap >= 10 {
                        let name = intelligentBreakName(startMin: endMin, endMin: nextStartMin, durationMin: gap)
                        let breakId = "break_\(current.endTime)_\(next.startTime)"
                        items.append(.breakBlock(
                            id: breakId,
                            name: name,
                            startTime: current.endTime,
                            endTime: next.startTime,
                            durationMinutes: gap
                        ))
                    }
                }
            }
        }

        return items
    }

    private static func intelligentBreakName(startMin: Int, endMin: Int, durationMin: Int) -> String {
        // e.g. 07:00 to 08:50 (Morning prep)
        if startMin < 8 * 60 {
            return "Morning Preparation"
        }
        // e.g. 10:00 to 11:30 (Recess)
        if startMin >= 10 * 60 && startMin < 12 * 60 {
            return "Recess"
        }
        // e.g. 12:00 to 14:00 (Lunch & Namaz)
        if startMin >= 12 * 60 && startMin < 14 * 60 {
            return "Lunch & Namaz Break"
        }
        // e.g. afternoon
        if startMin >= 14 * 60 {
            return "Afternoon Break"
        }
        return "Break"
    }

    /// Splits items into two horizontal rows according to day schedule rules (Mon-Thu, Fri, Sat)
    public static func splitIntoTwoHorizontalRows(
        items: [ScheduleTimelineItem],
        day: DayOfWeek
    ) -> (row1: [ScheduleTimelineItem], row2: [ScheduleTimelineItem]) {
        guard !items.isEmpty else { return ([], []) }

        switch day {
        case .monday, .tuesday, .wednesday, .thursday, .friday:
            // Find the index of Period 5 to split cleanly
            if let p5Index = items.firstIndex(where: {
                if case .classPeriod(let p, _) = $0 {
                    return p.periodName.contains("5")
                }
                return false
            }) {
                let splitPoint = p5Index + 1
                let row1 = Array(items[0..<splitPoint])
                let row2 = Array(items[splitPoint..<items.count])
                return (row1, row2)
            }

        case .saturday:
            // On Saturday, Row 1 includes Periods 1–4 and the Recess break right after Period 4.
            // Row 2 begins with Period 5 and continues through Period 8.
            if let p4Index = items.firstIndex(where: {
                if case .classPeriod(let p, _) = $0 {
                    return p.periodName.contains("4")
                }
                return false
            }) {
                var splitPoint = p4Index + 1
                if splitPoint < items.count, case .breakBlock = items[splitPoint] {
                    splitPoint += 1
                }
                let row1 = Array(items[0..<splitPoint])
                let row2 = Array(items[splitPoint..<items.count])
                return (row1, row2)
            }

        case .sunday:
            return (items, [])
        }

        // Fallback: split near the middle
        let mid = (items.count + 1) / 2
        let row1 = Array(items[0..<mid])
        let row2 = Array(items[mid..<items.count])
        return (row1, row2)
    }

    /// Divides timeline items into two balanced columns (Morning / Afternoon)
    /// to ensure zero vertical scrolling on standard viewports.
    public static func splitIntoTwoColumns(items: [ScheduleTimelineItem]) -> (morning: [ScheduleTimelineItem], afternoon: [ScheduleTimelineItem]) {
        guard !items.isEmpty else { return ([], []) }

        // Try to split at a major break (Lunch & Namaz or around noon)
        if let lunchIndex = items.firstIndex(where: {
            if case .breakBlock(_, let name, _, _, _) = $0, name.localizedCaseInsensitiveContains("Lunch") {
                return true
            }
            return false
        }) {
            // Include morning up to and including lunch break or split right after lunch
            let splitPoint = lunchIndex + 1
            let morning = Array(items[0..<splitPoint])
            let afternoon = Array(items[splitPoint..<items.count])
            return (morning, afternoon)
        }

        // Fallback: split near the middle
        let mid = (items.count + 1) / 2
        let morning = Array(items[0..<mid])
        let afternoon = Array(items[mid..<items.count])
        return (morning, afternoon)
    }
}

public struct DayScheduleRule: Sendable, Equatable {
    public let day: DayOfWeek
    public let hasPhysicalTraining: Bool
    public let isHalfDay: Bool
    public let daySubtitle: String
    public let row1Title: String
    public let row2Title: String

    public static func rule(for day: DayOfWeek) -> DayScheduleRule {
        switch day {
        case .monday, .tuesday, .wednesday, .thursday:
            return DayScheduleRule(
                day: day,
                hasPhysicalTraining: true,
                isHalfDay: false,
                daySubtitle: "Full Academic Schedule • 10 Periods",
                row1Title: "Morning Prep & Early Sessions (Periods 1–5)",
                row2Title: "Midday & Afternoon Sessions (Periods 6–10)"
            )
        case .friday:
            return DayScheduleRule(
                day: day,
                hasPhysicalTraining: false,
                isHalfDay: false,
                daySubtitle: "Jumua Mubarak • No Morning PT (Periods 2–10)",
                row1Title: "Morning Sessions (Periods 2–5)",
                row2Title: "Midday & Post-Jumua Sessions (Periods 6–10)"
            )
        case .saturday:
            return DayScheduleRule(
                day: day,
                hasPhysicalTraining: false,
                isHalfDay: true,
                daySubtitle: "Saturday Half-Day • Concludes at 1:15 PM",
                row1Title: "Early Morning Sessions (Periods 1–4)",
                row2Title: "Late Morning Sessions (Periods 5–8)"
            )
        case .sunday:
            return DayScheduleRule(
                day: day,
                hasPhysicalTraining: false,
                isHalfDay: true,
                daySubtitle: "Weekend • No Classes Scheduled",
                row1Title: "Weekend",
                row2Title: ""
            )
        }
    }
}

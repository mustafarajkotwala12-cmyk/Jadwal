import Foundation

public struct TimetableDiffEngine: Sendable {
    public init() {}

    /// Compares oldSnapshot vs newSnapshot and produces classified change records.
    public func diff(
        oldSnapshot: TimetableSnapshot?,
        newSnapshot: TimetableSnapshot
    ) -> [TimetableChangeRecord] {
        guard let oldSnapshot = oldSnapshot, !oldSnapshot.periods.isEmpty else {
            return []
        }

        // If academic year or week number changed, this is a new schedule import, not in-place adjustments
        if let oldWeek = oldSnapshot.weekNumber, let newWeek = newSnapshot.weekNumber, oldWeek != newWeek {
            return []
        }

        var changes: [TimetableChangeRecord] = []

        let oldDict: [String: PeriodOccurrence] = Dictionary(
            oldSnapshot.periods.map { ($0.id, $0) },
            uniquingKeysWith: { first, _ in first }
        )
        let newDict: [String: PeriodOccurrence] = Dictionary(
            newSnapshot.periods.map { ($0.id, $0) },
            uniquingKeysWith: { first, _ in first }
        )

        // 1. Detect updates and additions
        for newPeriod in newSnapshot.periods {
            if let oldPeriod = oldDict[newPeriod.id] {
                // Subject changed
                if newPeriod.subject != oldPeriod.subject {
                    changes.append(
                        TimetableChangeRecord(
                            periodId: newPeriod.id,
                            day: newPeriod.day,
                            dateString: newPeriod.dateString,
                            periodName: newPeriod.periodName,
                            changeType: .subjectChanged,
                            oldSubject: oldPeriod.subject,
                            newSubject: newPeriod.subject,
                            oldStartTime: oldPeriod.startTime,
                            newStartTime: newPeriod.startTime,
                            oldEndTime: oldPeriod.endTime,
                            newEndTime: newPeriod.endTime,
                            oldDetails: oldPeriod.details,
                            newDetails: newPeriod.details
                        )
                    )
                }

                // Time changed
                if newPeriod.startTime != oldPeriod.startTime || newPeriod.endTime != oldPeriod.endTime {
                    changes.append(
                        TimetableChangeRecord(
                            periodId: newPeriod.id,
                            day: newPeriod.day,
                            dateString: newPeriod.dateString,
                            periodName: newPeriod.periodName,
                            changeType: .timeChanged,
                            oldSubject: oldPeriod.subject,
                            newSubject: newPeriod.subject,
                            oldStartTime: oldPeriod.startTime,
                            newStartTime: newPeriod.startTime,
                            oldEndTime: oldPeriod.endTime,
                            newEndTime: newPeriod.endTime,
                            oldDetails: oldPeriod.details,
                            newDetails: newPeriod.details
                        )
                    )
                }

                // Details changed (only if subject didn't also change)
                if newPeriod.subject == oldPeriod.subject && newPeriod.details != oldPeriod.details && !newPeriod.details.isEmpty {
                    changes.append(
                        TimetableChangeRecord(
                            periodId: newPeriod.id,
                            day: newPeriod.day,
                            dateString: newPeriod.dateString,
                            periodName: newPeriod.periodName,
                            changeType: .detailsChanged,
                            oldSubject: oldPeriod.subject,
                            newSubject: newPeriod.subject,
                            oldStartTime: oldPeriod.startTime,
                            newStartTime: newPeriod.startTime,
                            oldEndTime: oldPeriod.endTime,
                            newEndTime: newPeriod.endTime,
                            oldDetails: oldPeriod.details,
                            newDetails: newPeriod.details
                        )
                    )
                }
            } else {
                // Newly added period slot
                changes.append(
                    TimetableChangeRecord(
                        periodId: newPeriod.id,
                        day: newPeriod.day,
                        dateString: newPeriod.dateString,
                        periodName: newPeriod.periodName,
                        changeType: .added,
                        newSubject: newPeriod.subject,
                        newStartTime: newPeriod.startTime,
                        newEndTime: newPeriod.endTime,
                        newDetails: newPeriod.details
                    )
                )
            }
        }

        // 2. Detect removed periods
        for oldPeriod in oldSnapshot.periods {
            if newDict[oldPeriod.id] == nil {
                changes.append(
                    TimetableChangeRecord(
                        periodId: oldPeriod.id,
                        day: oldPeriod.day,
                        dateString: oldPeriod.dateString,
                        periodName: oldPeriod.periodName,
                        changeType: .removed,
                        oldSubject: oldPeriod.subject,
                        oldStartTime: oldPeriod.startTime,
                        oldEndTime: oldPeriod.endTime,
                        oldDetails: oldPeriod.details
                    )
                )
            }
        }

        // If a massive number of periods changed (e.g. initial baseline import or format migration),
        // do not treat the whole timetable as changes.
        if changes.count > 15 {
            return []
        }

        return changes
    }

    /// Attaches change records to the matching periods in a snapshot for instant UI rendering.
    public func annotateSnapshot(
        _ snapshot: TimetableSnapshot,
        with changes: [TimetableChangeRecord]
    ) -> TimetableSnapshot {
        let changeMap: [String: TimetableChangeRecord] = Dictionary(
            changes.filter { !$0.isAcknowledged }.map { ($0.periodId, $0) },
            uniquingKeysWith: { current, _ in current }
        )

        let annotatedPeriods = snapshot.periods.map { period -> PeriodOccurrence in
            var p = period
            p.changeRecord = changeMap[period.id]
            return p
        }

        return TimetableSnapshot(
            academicYear: snapshot.academicYear,
            weekNumber: snapshot.weekNumber,
            startDate: snapshot.startDate,
            endDate: snapshot.endDate,
            periods: annotatedPeriods
        )
    }
}

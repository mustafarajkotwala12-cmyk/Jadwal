import Testing
import Foundation
@testable import JadwalCore

@Suite("JadwalCore Domain Tests")
struct JadwalCoreTests {
    @Test("PeriodOccurrence generates stable deterministic slot ID")
    func testPeriodOccurrenceId() {
        let p = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "08:50",
            endTime: "09:25",
            subject: "الرسالة الشريفة",
            details: "خيمة"
        )
        #expect(p.id == "2026-09-07_Period_2")
    }

    @Test("TimetableDiffEngine detects subject and time changes accurately")
    func testDiffEngineChanges() {
        let p1Old = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 1",
            startTime: "08:00",
            endTime: "08:45",
            subject: "Arabic",
            details: "Room 101"
        )
        let p2Old = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "08:50",
            endTime: "09:25",
            subject: "Linguistics",
            details: "Room 102"
        )

        let oldSnapshot = TimetableSnapshot(
            academicYear: "1447-1448",
            weekNumber: 25,
            periods: [p1Old, p2Old]
        )

        // New snapshot: Period 2 subject changed to Economics, Period 1 time changed
        let p1New = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 1",
            startTime: "08:15",
            endTime: "09:00",
            subject: "Arabic",
            details: "Room 101"
        )
        let p2New = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "09:05",
            endTime: "09:40",
            subject: "Economics",
            details: "Room 102"
        )
        let p3New = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 3",
            startTime: "10:00",
            endTime: "10:45",
            subject: "Mathematics",
            details: "Room 103"
        )

        let newSnapshot = TimetableSnapshot(
            academicYear: "1447-1448",
            weekNumber: 25,
            periods: [p1New, p2New, p3New]
        )

        let engine = TimetableDiffEngine()
        let changes = engine.diff(oldSnapshot: oldSnapshot, newSnapshot: newSnapshot)

        // Should detect:
        // Period 1: timeChanged
        // Period 2: subjectChanged
        // Period 3: added
        let p1Change = changes.first(where: { $0.periodId == p1New.id })
        #expect(p1Change?.changeType == .timeChanged)
        #expect(p1Change?.oldStartTime == "08:00")
        #expect(p1Change?.newStartTime == "08:15")

        let p2Change = changes.first(where: { $0.periodId == p2New.id })
        #expect(p2Change?.changeType == .subjectChanged)
        #expect(p2Change?.oldSubject == "Linguistics")
        #expect(p2Change?.newSubject == "Economics")

        let p3Change = changes.first(where: { $0.periodId == p3New.id })
        #expect(p3Change?.changeType == .added)
        #expect(p3Change?.newSubject == "Mathematics")

        // Annotate test
        let annotated = engine.annotateSnapshot(newSnapshot, with: changes)
        let annotatedP2 = annotated.periods.first(where: { $0.id == p2New.id })
        #expect(annotatedP2?.changeRecord?.changeType == .subjectChanged)
    }

    @Test("TaskDiscrepancy preserves task integrity and supports resolution actions")
    func testTaskDiscrepancyProtection() {
        var task = TaskItem(
            title: "Study Chapter 4",
            notes: "Focus on syntax",
            linkedSubject: "Linguistics",
            linkedPeriodId: "2026-09-07_Period_2"
        )

        task.discrepancy = TaskDiscrepancy(
            originalSubject: "Linguistics",
            newSubject: "Economics",
            periodId: "2026-09-07_Period_2"
        )

        // Non-destructive: title and notes are untouched
        #expect(task.title == "Study Chapter 4")
        #expect(task.linkedSubject == "Linguistics")
        #expect(task.discrepancy?.isDismissed == false)

        // Adopt new subject
        task.adoptNewSubject()
        #expect(task.linkedSubject == "Economics")
        #expect(task.discrepancy == nil)

        // Reset and test keep original
        task.linkedSubject = "Linguistics"
        task.discrepancy = TaskDiscrepancy(
            originalSubject: "Linguistics",
            newSubject: "Economics",
            periodId: "2026-09-07_Period_2"
        )
        task.keepOriginalContext()
        #expect(task.linkedSubject == "Linguistics")
        #expect(task.discrepancy?.isDismissed == true)
    }

    @Test("TaskItem status toggles correctly")
    func testTaskCompletionToggle() {
        var task = TaskItem(title: "Prepare notes")
        #expect(!task.isCompleted)
        task.markCompleted()
        #expect(task.isCompleted)
        #expect(task.completedAt != nil)
        task.reopen()
        #expect(!task.isCompleted)
        #expect(task.completedAt == nil)
    }

    @Test("ScheduleTimelineBuilder detects breaks and computes statuses")
    func testScheduleTimelineBuilder() {
        let p1 = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 1",
            startTime: "06:00",
            endTime: "07:00",
            subject: "Quran",
            details: "Masjid"
        )
        let p2 = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "08:50",
            endTime: "09:25",
            subject: "Fiqh",
            details: "Room 101"
        )
        let p3 = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 3",
            startTime: "09:25",
            endTime: "10:00",
            subject: "Adab",
            details: "Room 102"
        )
        let p4 = PeriodOccurrence(
            day: .monday,
            dateString: "2026-09-07",
            periodName: "Period 4",
            startTime: "12:40",
            endTime: "13:15",
            subject: "Hadith",
            details: "Room 103"
        )

        let items = ScheduleTimelineBuilder.buildTimeline(from: [p1, p2, p3, p4])
        // Expected items:
        // 1. p1 (06:00 - 07:00)
        // 2. Break (07:00 - 08:50) -> Morning Preparation (1h 50m)
        // 3. p2 (08:50 - 09:25)
        // 4. p3 (09:25 - 10:00, no break)
        // 5. Break (10:00 - 12:40) -> Recess
        // 6. p4 (12:40 - 13:15)
        #expect(items.count == 6)

        if case .breakBlock(_, let name, let start, let end, let dur) = items[1] {
            #expect(name == "Morning Preparation")
            #expect(start == "07:00")
            #expect(end == "08:50")
            #expect(dur == 110)
            #expect(ScheduleTimelineBuilder.formatDuration(minutes: dur) == "1h 50m")
        } else {
            #expect(Bool(false), "Expected breakBlock at index 1")
        }

        let columns = ScheduleTimelineBuilder.splitIntoTwoColumns(items: items)
        #expect(!columns.morning.isEmpty)
        #expect(!columns.afternoon.isEmpty)
    }
}

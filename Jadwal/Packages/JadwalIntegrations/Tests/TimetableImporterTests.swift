import Testing
import Foundation
@testable import JadwalCore
@testable import JadwalIntegrations

@Suite("Timetable Importer Tests")
struct TimetableImporterTests {
    @Test("Parse sample timetable JSON correctly")
    func testSamplePayloadParsing() throws {
        let sampleJSON = """
        {
          "academicYear": "1447 / 1448",
          "weekNumber": 25,
          "startDate": "2026-09-07",
          "endDate": "2026-09-13",
          "entries": [
            {
              "day": "Monday",
              "date": "2026-09-07",
              "period": "Period 2",
              "startTime": "08:50",
              "endTime": "09:25",
              "subject": "الرسالة الشريفة (ب)",
              "details": "خيمة الرياضة"
            },
            {
              "day": "Saturday",
              "date": "2026-09-12",
              "period": "Period 1",
              "startTime": "08:15",
              "endTime": "08:50",
              "subject": "نهج البلاغة",
              "details": "شيخ"
            }
          ]
        }
        """.data(using: .utf8)!

        let importer = TimetableImporter()
        let snapshot = try importer.importFromJSONData(sampleJSON)

        #expect(snapshot.academicYear == "1447 / 1448")
        #expect(snapshot.weekNumber == 25)
        #expect(snapshot.periods.count == 2)
        #expect(snapshot.periods(for: .monday).count == 1)
        #expect(snapshot.periods(for: .saturday).first?.startTime == "08:15")
    }

    @Test("Load and normalize actual timetable.json from disk")
    func testLiveTimetableFile() throws {
        let bridge = JameaHelperBridge()
        let snapshot = try bridge.loadCurrentSnapshot()

        #expect(snapshot.academicYear == "1447 / 1448")
        #expect(snapshot.weekNumber == 25)
        #expect(snapshot.periods.count == 57)
        #expect(snapshot.periods(for: .monday).count == 10)
        #expect(snapshot.periods(for: .saturday).count == 8)
        #expect(snapshot.periods(for: .saturday).first?.startTime == "08:15")
    }
}

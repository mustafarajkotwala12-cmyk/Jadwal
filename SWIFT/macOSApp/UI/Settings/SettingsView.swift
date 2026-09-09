import SwiftUI
import SwiftCore

public struct SettingsView: View {
    @EnvironmentObject var env: AppEnvironment

    public init() {}

    public var body: some View {
        Form {
            Section(header: Text("Jamea Helper Integration")) {
                LabeledContent("Status") {
                    HStack {
                        Circle()
                            .fill(env.timetableSnapshot != nil ? Color.green : Color.orange)
                            .frame(width: 8, height: 8)
                        Text(env.timetableSnapshot != nil ? "Connected & Loaded" : "No Data Loaded")
                            .font(.callout)
                    }
                }

                if let snapshot = env.timetableSnapshot {
                    LabeledContent("Academic Year", value: snapshot.academicYear)
                    if let week = snapshot.weekNumber {
                        LabeledContent("Current Week", value: "Week #\(week)")
                    }
                    if let start = snapshot.startDate, let end = snapshot.endDate {
                        LabeledContent("Effective Dates", value: "\(start) → \(end)")
                    }
                    LabeledContent("Total Periods", value: "\(snapshot.periods.count) periods (Mon–Sat)")
                    LabeledContent("Total Unique Subjects", value: "\(snapshot.uniqueSubjects.count)")
                }

                Button(action: {
                    Task { await env.refreshTimetable() }
                }) {
                    if env.isSyncing {
                        ProgressView()
                            .scaleEffect(0.8)
                    } else {
                        Label("Refresh Timetable via Helper", systemImage: "arrow.triangle.2.circlepath")
                    }
                }
                .disabled(env.isSyncing)

                if let status = env.statusMessage {
                    Text(status)
                        .font(.caption)
                        .foregroundStyle(.green)
                }

                if let error = env.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }

            Section(header: Text("About SWIFT")) {
                LabeledContent("Version", value: "1.0.0 (MVP)")
                LabeledContent("Architecture", value: "Core + Native macOS Shell")
                LabeledContent("Storage", value: "Local Atomic Persistence")
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Settings")
    }
}

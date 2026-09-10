import SwiftUI
import JadwalCore

public struct MenuBarView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var quickTaskTitle: String = ""
    @Environment(\.openWindow) var openWindow

    public init() {}

    private var currentDayOfWeek: DayOfWeek {
        let weekday = Calendar.current.component(.weekday, from: Date())
        switch weekday {
        case 1: return .sunday
        case 2: return .monday
        case 3: return .tuesday
        case 4: return .wednesday
        case 5: return .thursday
        case 6: return .friday
        case 7: return .saturday
        default: return .monday
        }
    }

    private var todayPeriods: [PeriodOccurrence] {
        env.timetableSnapshot?.periods(for: currentDayOfWeek) ?? []
    }

    private var nextUpcomingPeriod: PeriodOccurrence? {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        let nowString = formatter.string(from: Date())

        // Find current or next period today
        return todayPeriods.first(where: { $0.endTime >= nowString }) ?? todayPeriods.first
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            // Header
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Jadwal")
                        .font(.headline.bold())
                    if let week = env.timetableSnapshot?.weekNumber {
                        Text("Week #\(week) • \(currentDayOfWeek.rawValue)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }

                Spacer()

                Button(action: {
                    Task { await env.refreshTimetable() }
                }) {
                    Image(systemName: "arrow.triangle.2.circlepath")
                        .font(.caption)
                }
                .buttonStyle(.plain)
                .disabled(env.isSyncing)
                .help("Sync Timetable")
            }

            Divider()

            // Next / Current Class Card
            VStack(alignment: .leading, spacing: 6) {
                Text("NEXT / CURRENT CLASS")
                    .font(.caption2.bold())
                    .foregroundStyle(.secondary)

                if let period = nextUpcomingPeriod {
                    HStack(spacing: 12) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(period.startTime)
                                .font(.subheadline.bold().monospacedDigit())
                            Text(period.endTime)
                                .font(.caption2.monospacedDigit())
                                .foregroundStyle(.secondary)
                        }
                        .frame(width: 55, alignment: .leading)

                        VStack(alignment: .leading, spacing: 2) {
                            Text(period.subject)
                                .font(period.subject.containsArabic ? .kanzalLulu(size: 16) : .body.weight(.medium))
                                .lineLimit(1)
                            if let change = period.changeRecord {
                                HStack(spacing: 3) {
                                    Image(systemName: "exclamationmark.triangle.fill")
                                    Text(change.summaryMessage)
                                }
                                .font(.caption2.bold())
                                .foregroundStyle(.orange)
                                .lineLimit(1)
                            } else if !period.details.isEmpty {
                                Text(period.details)
                                    .font(period.details.containsArabic ? .kanzalLulu(size: 12) : .caption2)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(1)
                            }
                        }

                        Spacer()

                        Text(period.periodName)
                            .font(.caption2.bold())
                            .padding(.horizontal, 6)
                            .padding(.vertical, 3)
                            .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                    }
                    .padding(10)
                    .background(RoundedRectangle(cornerRadius: 8).fill(Color(NSColor.controlBackgroundColor)))
                } else {
                    Text("No upcoming classes today")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .padding(.vertical, 4)
                }
            }

            Divider()

            // Quick Task Entry
            HStack(spacing: 8) {
                Image(systemName: "plus.circle.fill")
                    .foregroundStyle(Color.accentColor)
                TextField("Quick task...", text: $quickTaskTitle)
                    .textFieldStyle(.plain)
                    .onSubmit {
                        let trimmed = quickTaskTitle.trimmingCharacters(in: .whitespaces)
                        guard !trimmed.isEmpty else { return }
                        Task {
                            await env.addTask(title: trimmed)
                            quickTaskTitle = ""
                        }
                    }
            }
            .padding(8)
            .background(RoundedRectangle(cornerRadius: 6).fill(Color(NSColor.controlBackgroundColor)))

            // Pending Tasks
            let pendingTasks = Array(env.tasks.filter { !$0.isCompleted }.prefix(4))
            if !pendingTasks.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Text("TASKS")
                        .font(.caption2.bold())
                        .foregroundStyle(.secondary)

                    ForEach(pendingTasks) { task in
                        HStack(spacing: 8) {
                            Button(action: {
                                Task { await env.toggleTask(task) }
                            }) {
                                Image(systemName: "circle")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            .buttonStyle(.plain)

                            Text(task.title)
                                .font(task.title.containsArabic ? .kanzalLulu(size: 14) : .callout)
                                .lineLimit(1)

                            Spacer()

                            if let subj = task.linkedSubject, !subj.isEmpty {
                                Text(subj)
                                    .font(subj.containsArabic ? .kanzalLulu(size: 11) : .caption2)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(1)
                            }
                        }
                        .padding(.vertical, 2)
                    }
                }
            }

            Divider()

            // Footer Actions
            HStack {
                Button("Open Jadwal") {
                    NSApplication.shared.activate(ignoringOtherApps: true)
                    for window in NSApplication.shared.windows {
                        window.makeKeyAndOrderFront(nil)
                    }
                }
                .buttonStyle(.link)
                .font(.caption.bold())

                Spacer()

                Button("Quit") {
                    NSApplication.shared.terminate(nil)
                }
                .buttonStyle(.plain)
                .font(.caption)
                .foregroundStyle(.secondary)
            }
        }
        .padding(14)
        .frame(width: 330)
    }
}

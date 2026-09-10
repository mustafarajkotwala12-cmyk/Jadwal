import SwiftUI
import JadwalCore

public struct TimetableView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var selectedDay: DayOfWeek = .monday
    @State private var showingAddTaskForPeriod: PeriodOccurrence? = nil
    @State private var newTaskTitle: String = ""

    public init() {}

    public var body: some View {
        VStack(spacing: 0) {
            // Day Selector Picker
            HStack {
                Picker("Day", selection: $selectedDay) {
                    ForEach([DayOfWeek.monday, .tuesday, .wednesday, .thursday, .friday, .saturday], id: \.self) { day in
                        Text("\(day.rawValue)")
                            .tag(day)
                    }
                }
                .pickerStyle(.segmented)
                .padding(.horizontal)
                .padding(.vertical, 10)

                Spacer()

                Button(action: {
                    Task { await env.refreshTimetable() }
                }) {
                    Label("Sync", systemImage: "arrow.triangle.2.circlepath")
                }
                .disabled(env.isSyncing)
                .padding(.trailing)
            }
            .background(Color(NSColor.windowBackgroundColor))

            Divider()

            // Periods List
            let dayPeriods = env.timetableSnapshot?.periods(for: selectedDay) ?? []

            if dayPeriods.isEmpty {
                ContentUnavailableView(
                    "No Classes Scheduled",
                    systemImage: "calendar.badge.exclamationmark",
                    description: Text("No periods found for \(selectedDay.rawValue). Ensure your timetable is synced.")
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 12) {
                        ForEach(dayPeriods) { period in
                            PeriodCardView(period: period) {
                                showingAddTaskForPeriod = period
                            }
                        }
                    }
                    .padding()
                }
            }
        }
        .navigationTitle("Weekly Timetable")
        .sheet(item: $showingAddTaskForPeriod) { period in
            AddTaskModal(period: period) { title in
                Task {
                    await env.addTask(
                        title: title,
                        linkedSubject: period.subject,
                        linkedPeriodId: period.id
                    )
                }
            }
        }
    }
}

struct PeriodCardView: View {
    let period: PeriodOccurrence
    let onAddTask: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 16) {
            // Time Badge
            VStack(alignment: .leading, spacing: 4) {
                Text(period.periodName)
                    .font(.caption.bold())
                    .foregroundStyle(.secondary)
                Text(period.startTime)
                    .font(.headline)
                Text(period.endTime)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .frame(width: 80, alignment: .leading)

            Divider()

            // Subject & Teacher Details
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: 8) {
                    Text(period.subject)
                        .font(period.subject.containsArabic ? .kanzalLulu(size: 20) : .title3.weight(.medium))
                        .foregroundStyle(.primary)

                    if let change = period.changeRecord {
                        HStack(spacing: 3) {
                            Image(systemName: "exclamationmark.triangle.fill")
                            Text(change.summaryMessage)
                        }
                        .font(.caption2.bold())
                        .foregroundStyle(.orange)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(Capsule().fill(Color.orange.opacity(0.15)))
                    }
                }

                if !period.details.isEmpty {
                    Label(period.details, systemImage: "person.text.rectangle")
                        .font(period.details.containsArabic ? .kanzalLulu(size: 13) : .subheadline)
                        .foregroundStyle(.secondary)
                }
            }

            Spacer()

            // Action Button
            Button(action: onAddTask) {
                Image(systemName: "plus.circle")
                    .font(.title3)
            }
            .buttonStyle(.plain)
            .help("Attach task to this class")
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(NSColor.controlBackgroundColor))
                .shadow(color: .black.opacity(0.04), radius: 2, y: 1)
        )
    }
}

struct AddTaskModal: View {
    let period: PeriodOccurrence
    let onAdd: (String) -> Void
    @Environment(\.dismiss) var dismiss
    @State private var title: String = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Add Task for \(period.subject)")
                .font(period.subject.containsArabic ? .kanzalLulu(size: 17) : .headline)

            TextField("Task description...", text: $title)
                .textFieldStyle(.roundedBorder)

            HStack {
                Button("Cancel", role: .cancel) {
                    dismiss()
                }
                Spacer()
                Button("Add Task") {
                    guard !title.trimmingCharacters(in: .whitespaces).isEmpty else { return }
                    onAdd(title)
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
            }
        }
        .padding()
        .frame(width: 360)
    }
}

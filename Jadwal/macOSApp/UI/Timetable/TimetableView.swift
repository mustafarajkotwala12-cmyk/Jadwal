import SwiftUI
import JadwalCore

public struct TimetableView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var selectedDay: DayOfWeek = .monday
    @State private var showingAddTaskForPeriod: PeriodOccurrence? = nil
    @State private var newTaskTitle: String = ""
    @State private var currentTime: Date = Date()
    private let liveTicker = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

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

            // Timetable Content for Selected Day
            let dayPeriods = env.timetableSnapshot?.periods(for: selectedDay) ?? []
            let rule = DayScheduleRule.rule(for: selectedDay)
            let timelineItems = ScheduleTimelineBuilder.buildTimeline(from: dayPeriods, currentTime: currentTime)
            let rowSplit = ScheduleTimelineBuilder.splitIntoTwoHorizontalRows(items: timelineItems, day: selectedDay)

            if dayPeriods.isEmpty {
                ContentUnavailableView(
                    "No Classes Scheduled",
                    systemImage: "calendar.badge.exclamationmark",
                    description: Text("No periods found for \(selectedDay.rawValue). Ensure your timetable is synced.")
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView(.vertical, showsIndicators: true) {
                    VStack(alignment: .leading, spacing: 16) {
                        // Date Banner matching Freeform sketch
                        TimetableDateBanner(
                            dayOfWeek: selectedDay,
                            date: currentTime,
                            todayPeriods: dayPeriods,
                            rule: rule
                        )

                        // Row 1: Morning Sessions & Breaks
                        if !rowSplit.row1.isEmpty {
                            VStack(alignment: .leading, spacing: 8) {
                                rowHeader(
                                    title: rule.row1Title,
                                    icon: "sun.and.horizon.fill",
                                    color: FatimidPalette.emerald
                                )

                                ScrollView(.horizontal, showsIndicators: false) {
                                    HStack(alignment: .top, spacing: 10) {
                                        ForEach(rowSplit.row1) { item in
                                            renderTimelineItem(item)
                                        }
                                    }
                                    .padding(.vertical, 4)
                                    .padding(.horizontal, 2)
                                }
                            }
                        }

                        // Row 2: Midday / Afternoon Sessions & Breaks
                        if !rowSplit.row2.isEmpty {
                            VStack(alignment: .leading, spacing: 8) {
                                rowHeader(
                                    title: rule.row2Title,
                                    icon: "sun.max.fill",
                                    color: FatimidPalette.bronze
                                )

                                ScrollView(.horizontal, showsIndicators: false) {
                                    HStack(alignment: .top, spacing: 10) {
                                        ForEach(rowSplit.row2) { item in
                                            renderTimelineItem(item)
                                        }
                                    }
                                    .padding(.vertical, 4)
                                    .padding(.horizontal, 2)
                                }
                            }
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 16)
                }
            }
        }
        .navigationTitle("Weekly Timetable")
        .onReceive(liveTicker) { date in
            currentTime = date
        }
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

    // MARK: - Row Header
    private func rowHeader(title: String, icon: String, color: Color) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(color)

            Text(title.uppercased())
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .foregroundStyle(.secondary)
                .tracking(0.6)

            Spacer()
        }
        .padding(.horizontal, 2)
    }

    // MARK: - Timeline Item Renderer
    @ViewBuilder
    private func renderTimelineItem(_ item: ScheduleTimelineItem) -> some View {
        switch item {
        case .classPeriod(let period, let status):
            ClassCardView(period: period, status: status)
                .frame(width: 175, height: 215)
        case .breakBlock(_, let name, let startTime, let endTime, let durationMinutes):
            VerticalBreakPillView(
                name: name,
                startTime: startTime,
                endTime: endTime,
                durationMinutes: durationMinutes
            )
            .frame(width: 54, height: 215)
        }
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

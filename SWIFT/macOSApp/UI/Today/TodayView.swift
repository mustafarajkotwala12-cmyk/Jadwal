import SwiftUI
import SwiftCore

public struct TodayView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var showingQuickAddTask: Bool = false
    @State private var quickTaskTitle: String = ""

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

    private var currentOrNextClass: (period: PeriodOccurrence, isCurrent: Bool)? {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        let now = formatter.string(from: Date())

        if let active = todayPeriods.first(where: { now >= $0.startTime && now <= $0.endTime }) {
            return (active, true)
        }
        if let upcoming = todayPeriods.first(where: { now < $0.startTime }) {
            return (upcoming, false)
        }
        return nil
    }

    public var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                // Header Banner
                HStack(alignment: .bottom) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(Date().formatted(date: .complete, time: .omitted))
                            .font(.subheadline.weight(.medium))
                            .foregroundStyle(.secondary)
                        Text("Today's Overview")
                            .font(.system(.largeTitle, design: .rounded).bold())
                    }

                    Spacer()

                    if let snapshot = env.timetableSnapshot {
                        HStack(spacing: 8) {
                            VStack(alignment: .trailing, spacing: 2) {
                                Text(snapshot.academicYear)
                                    .font(.caption.bold())
                                if let week = snapshot.weekNumber {
                                    Text("Week #\(week)")
                                        .font(.caption)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(NSColor.controlBackgroundColor))
                            )
                        }
                    }
                }
                .padding(.bottom, 4)

                // Hero Card: Current or Next Class
                if let focus = currentOrNextClass {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Label(
                                focus.isCurrent ? "CLASS IN SESSION" : "NEXT UP",
                                systemImage: focus.isCurrent ? "record.circle" : "arrow.right.circle"
                            )
                            .font(.caption.bold())
                            .foregroundStyle(focus.isCurrent ? Color.green : Color.accentColor)

                            Spacer()

                            Text("\(focus.period.startTime) – \(focus.period.endTime)")
                                .font(.subheadline.bold().monospacedDigit())
                                .foregroundStyle(.secondary)
                        }

                        HStack(alignment: .top) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(focus.period.subject)
                                    .font(.title2.bold())
                                    .foregroundStyle(.primary)

                                if let change = focus.period.changeRecord {
                                    HStack(spacing: 4) {
                                        Image(systemName: "exclamationmark.triangle.fill")
                                        Text(change.summaryMessage)
                                    }
                                    .font(.caption.bold())
                                    .foregroundStyle(.orange)
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 3)
                                    .background(Capsule().fill(Color.orange.opacity(0.15)))
                                }

                                if !focus.period.details.isEmpty {
                                    Text(focus.period.details)
                                        .font(.subheadline)
                                        .foregroundStyle(.secondary)
                                }
                            }

                            Spacer()

                            Text(focus.period.periodName)
                                .font(.caption.bold())
                                .padding(.horizontal, 10)
                                .padding(.vertical, 4)
                                .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                        }
                    }
                    .padding(16)
                    .background(
                        RoundedRectangle(cornerRadius: 14)
                            .fill(Color(NSColor.controlBackgroundColor))
                            .overlay(
                                RoundedRectangle(cornerRadius: 14)
                                    .stroke(focus.isCurrent ? Color.green.opacity(0.4) : Color.accentColor.opacity(0.2), lineWidth: 1.5)
                            )
                            .shadow(color: .black.opacity(0.04), radius: 6, y: 2)
                    )
                }

                // Today's Changes Banner (if any period today was changed)
                let todayChangedPeriods = todayPeriods.compactMap { $0.changeRecord }
                if !todayChangedPeriods.isEmpty {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Image(systemName: "bell.badge.fill")
                                .foregroundStyle(.orange)
                            Text("Schedule Adjustments Today")
                                .font(.headline.bold())
                                .foregroundStyle(.orange)

                            Spacer()

                            Button("Dismiss") {
                                Task { await env.dismissAllChanges() }
                            }
                            .font(.caption.bold())
                            .buttonStyle(.bordered)
                            .controlSize(.small)
                        }

                        ForEach(todayChangedPeriods) { change in
                            HStack(spacing: 8) {
                                Image(systemName: "exclamationmark.circle")
                                    .foregroundStyle(.orange)
                                Text(change.summaryMessage)
                                    .font(.subheadline)
                            }
                        }
                    }
                    .padding(14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(
                        RoundedRectangle(cornerRadius: 10)
                            .fill(Color.orange.opacity(0.1))
                            .overlay(
                                RoundedRectangle(cornerRadius: 10)
                                    .stroke(Color.orange.opacity(0.25), lineWidth: 1)
                            )
                    )
                }

                // Today's Timetable Section
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Label("Today's Schedule (\(currentDayOfWeek.rawValue))", systemImage: "clock")
                            .font(.title3.bold())

                        Spacer()

                        Text("\(todayPeriods.count) periods")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }

                    if todayPeriods.isEmpty {
                        HStack {
                            Text("No classes scheduled for today.")
                                .foregroundStyle(.secondary)
                            Spacer()
                        }
                        .padding()
                        .background(RoundedRectangle(cornerRadius: 10).fill(Color(NSColor.controlBackgroundColor)))
                    } else {
                        ForEach(todayPeriods) { period in
                            HStack(spacing: 14) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(period.startTime)
                                        .font(.headline.monospacedDigit())
                                    Text(period.endTime)
                                        .font(.caption.monospacedDigit())
                                        .foregroundStyle(.secondary)
                                }
                                .frame(width: 60, alignment: .leading)

                                Divider()

                                VStack(alignment: .leading, spacing: 2) {
                                    HStack(spacing: 8) {
                                        Text(period.subject)
                                            .font(.body.weight(.medium))

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
                                        Text(period.details)
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                    }
                                }

                                Spacer()

                                Text(period.periodName)
                                    .font(.caption.bold())
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 3)
                                    .background(Capsule().fill(Color.secondary.opacity(0.12)))
                            }
                            .padding(12)
                            .background(
                                RoundedRectangle(cornerRadius: 10)
                                    .fill(Color(NSColor.controlBackgroundColor))
                            )
                        }
                    }
                }

                // Today's Tasks Section
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Label("Tasks & Action Items", systemImage: "checklist")
                            .font(.title3.bold())

                        Spacer()

                        Button(action: { showingQuickAddTask.toggle() }) {
                            Label("New Task", systemImage: "plus")
                                .font(.caption.bold())
                        }
                        .buttonStyle(.bordered)
                    }

                    if showingQuickAddTask {
                        HStack(spacing: 8) {
                            TextField("Enter task title and press Return...", text: $quickTaskTitle)
                                .textFieldStyle(.roundedBorder)
                                .onSubmit {
                                    let trimmed = quickTaskTitle.trimmingCharacters(in: .whitespaces)
                                    guard !trimmed.isEmpty else { return }
                                    Task {
                                        await env.addTask(title: trimmed)
                                        quickTaskTitle = ""
                                        showingQuickAddTask = false
                                    }
                                }

                            Button("Add") {
                                let trimmed = quickTaskTitle.trimmingCharacters(in: .whitespaces)
                                guard !trimmed.isEmpty else { return }
                                Task {
                                    await env.addTask(title: trimmed)
                                    quickTaskTitle = ""
                                    showingQuickAddTask = false
                                }
                            }
                            .buttonStyle(.borderedProminent)
                        }
                        .padding(10)
                        .background(RoundedRectangle(cornerRadius: 8).fill(Color(NSColor.controlBackgroundColor)))
                    }

                    let pendingTasks = env.tasks.filter { !$0.isCompleted }

                    if pendingTasks.isEmpty {
                        HStack {
                            Image(systemName: "checkmark.seal.fill")
                                .foregroundStyle(.green)
                            Text("All clear! No pending tasks.")
                                .foregroundStyle(.secondary)
                        }
                        .padding()
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(RoundedRectangle(cornerRadius: 10).fill(Color(NSColor.controlBackgroundColor)))
                    } else {
                        ForEach(pendingTasks) { task in
                            HStack(spacing: 12) {
                                Button(action: {
                                    Task { await env.toggleTask(task) }
                                }) {
                                    Image(systemName: "circle")
                                        .font(.title3)
                                        .foregroundStyle(.secondary)
                                }
                                .buttonStyle(.plain)

                                VStack(alignment: .leading, spacing: 2) {
                                    Text(task.title)
                                        .font(.body)
                                    HStack(spacing: 8) {
                                        if let subj = task.linkedSubject {
                                            Label(subj, systemImage: "tag")
                                                .font(.caption)
                                                .foregroundStyle(.secondary)
                                        }
                                        if let disc = task.discrepancy, !disc.isDismissed {
                                            Label("Slot changed to \(disc.newSubject)", systemImage: "exclamationmark.triangle.fill")
                                                .font(.caption2.bold())
                                                .foregroundStyle(.orange)
                                        }
                                    }
                                }

                                Spacer()

                                PriorityBadge(priority: task.priority)
                            }
                            .padding(12)
                            .background(RoundedRectangle(cornerRadius: 8).fill(Color(NSColor.controlBackgroundColor)))
                        }
                    }
                }
            }
            .padding(24)
        }
    }
}

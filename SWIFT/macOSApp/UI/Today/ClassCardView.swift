import SwiftUI
import SwiftCore

public struct ClassCardView: View {
    @EnvironmentObject var env: AppEnvironment
    @Environment(\.accessibilityReduceMotion) var reduceMotion

    public let period: PeriodOccurrence
    public let status: ClassLiveStatus

    @State private var isFlipped: Bool = false
    @State private var flipAngle: Double = 0
    @State private var newTaskTitle: String = ""
    @FocusState private var isFieldFocused: Bool

    public init(period: PeriodOccurrence, status: ClassLiveStatus) {
        self.period = period
        self.status = status
    }

    private var linkedTasks: [TaskItem] {
        env.tasks.filter {
            ($0.linkedPeriodId == period.id || ($0.linkedSubject != nil && $0.linkedSubject == period.subject)) && !$0.isCompleted
        }
    }

    private var completedLinkedTasks: [TaskItem] {
        env.tasks.filter {
            ($0.linkedPeriodId == period.id || ($0.linkedSubject != nil && $0.linkedSubject == period.subject)) && $0.isCompleted
        }
    }

    public var body: some View {
        ZStack {
            if flipAngle < 90 {
                frontCard
            } else {
                backCard
                    .rotation3DEffect(.degrees(180), axis: (x: 0, y: 1, z: 0))
            }
        }
        .rotation3DEffect(
            .degrees(flipAngle),
            axis: (x: 0, y: 1, z: 0),
            perspective: 0.7
        )
        .focusable()
        .onKeyPress(.return) {
            toggleFlip()
            return .handled
        }
        .onKeyPress(.space) {
            toggleFlip()
            return .handled
        }
    }

    private func toggleFlip() {
        if reduceMotion {
            isFlipped.toggle()
            flipAngle = isFlipped ? 180 : 0
        } else {
            withAnimation(.spring(response: 0.5, dampingFraction: 0.75, blendDuration: 0)) {
                isFlipped.toggle()
                flipAngle = isFlipped ? 180 : 0
            }
        }
    }

    // MARK: - Front Face
    private var frontCard: some View {
        VStack(alignment: .leading, spacing: 6) {
            // Top row: Times + Status + Period badge
            HStack(alignment: .center, spacing: 6) {
                // Time Range
                Text("\(period.startTime) – \(period.endTime)")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(status == .inProgress ? FatimidPalette.emerald : .secondary)

                Spacer()

                // Live status pill
                statusBadge

                // Period Badge
                Text(period.periodName)
                    .font(.system(size: 10, weight: .bold, design: .rounded))
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(
                        Capsule()
                            .fill(status == .inProgress ? FatimidPalette.emeraldSoft : Color.secondary.opacity(0.12))
                    )
                    .foregroundStyle(status == .inProgress ? FatimidPalette.emerald : .secondary)
            }

            // Subject name: Large, bold, readable from a distance
            HStack(alignment: .firstTextBaseline) {
                Text(period.subject)
                    .font(.system(size: 17, weight: .bold, design: .rounded))
                    .foregroundStyle(status == .completed ? .secondary : .primary)
                    .lineLimit(1)

                Spacer()

                // Linked tasks counter button that hints at flipping
                Button(action: toggleFlip) {
                    HStack(spacing: 4) {
                        Image(systemName: "checklist")
                            .font(.system(size: 10, weight: .semibold))
                        if !linkedTasks.isEmpty {
                            Text("\(linkedTasks.count)")
                                .font(.system(size: 10, weight: .bold))
                        }
                    }
                    .padding(.horizontal, 6)
                    .padding(.vertical, 3)
                    .background(
                        Capsule()
                            .fill(linkedTasks.isEmpty ? Color.secondary.opacity(0.08) : FatimidPalette.bronzeSoft)
                    )
                    .foregroundStyle(linkedTasks.isEmpty ? .secondary : FatimidPalette.bronze)
                }
                .buttonStyle(.plain)
                .help("Flip card to view tasks")
            }

            // Bottom row: Details / Ustaadh / Room + Change banner
            HStack(spacing: 8) {
                if !period.details.isEmpty {
                    HStack(spacing: 4) {
                        Image(systemName: "mappin.and.ellipse")
                            .font(.system(size: 9))
                            .foregroundStyle(.tertiary)
                        Text(period.details)
                            .font(.system(size: 11))
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }

                if let change = period.changeRecord, !change.isAcknowledged {
                    HStack(spacing: 3) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .font(.system(size: 9))
                        Text(change.changeType == .subjectChanged ? "Subject changed" : "Updated")
                            .font(.system(size: 9, weight: .bold))
                    }
                    .foregroundStyle(.orange)
                    .padding(.horizontal, 5)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.orange.opacity(0.15)))
                }

                Spacer()

                // Quick flip affordance icon
                Button(action: toggleFlip) {
                    Image(systemName: "arrow.triangle.2.circlepath")
                        .font(.system(size: 10))
                        .foregroundStyle(.tertiary)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(10)
        .background(cardBackground)
        .contentShape(Rectangle())
        .onTapGesture {
            toggleFlip()
        }
    }

    // MARK: - Back Face (Today's Tasks)
    private var backCard: some View {
        VStack(alignment: .leading, spacing: 6) {
            // Header Row
            HStack(alignment: .center) {
                HStack(spacing: 4) {
                    Image(systemName: "checklist")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(FatimidPalette.bronze)
                    Text("TASKS: \(period.subject.uppercased())")
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer()

                // Return/Flip button
                Button(action: toggleFlip) {
                    HStack(spacing: 3) {
                        Image(systemName: "arrow.uturn.backward")
                            .font(.system(size: 9, weight: .bold))
                        Text("Card")
                            .font(.system(size: 9, weight: .semibold))
                    }
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.secondary.opacity(0.12)))
                    .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }

            // Task List
            let allRelevant = linkedTasks + completedLinkedTasks
            if allRelevant.isEmpty {
                Text("No tasks linked to this class yet.")
                    .font(.system(size: 11))
                    .foregroundStyle(.tertiary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.vertical, 2)
            } else {
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(allRelevant.prefix(3)) { task in
                        HStack(spacing: 6) {
                            Button(action: {
                                Task { await env.toggleTask(task) }
                            }) {
                                Image(systemName: task.isCompleted ? "checkmark.circle.fill" : "circle")
                                    .font(.system(size: 11))
                                    .foregroundStyle(task.isCompleted ? FatimidPalette.emerald : .secondary)
                            }
                            .buttonStyle(.plain)

                            Text(task.title)
                                .font(.system(size: 11))
                                .strikethrough(task.isCompleted)
                                .foregroundStyle(task.isCompleted ? .tertiary : .primary)
                                .lineLimit(1)

                            Spacer()
                        }
                    }
                }
            }

            // Inline Quick Add Task
            HStack(spacing: 6) {
                TextField("Add task for \(period.subject)...", text: $newTaskTitle)
                    .textFieldStyle(.plain)
                    .font(.system(size: 11))
                    .padding(.horizontal, 6)
                    .padding(.vertical, 3)
                    .background(
                        RoundedRectangle(cornerRadius: 5)
                            .fill(Color(NSColor.textBackgroundColor).opacity(0.6))
                    )
                    .focused($isFieldFocused)
                    .onSubmit {
                        submitNewTask()
                    }

                Button(action: submitNewTask) {
                    Image(systemName: "plus.circle.fill")
                        .font(.system(size: 13))
                        .foregroundStyle(FatimidPalette.emerald)
                }
                .buttonStyle(.plain)
                .disabled(newTaskTitle.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
        .padding(10)
        .background(cardBackground)
    }

    private func submitNewTask() {
        let trimmed = newTaskTitle.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }
        Task {
            await env.addTask(
                title: trimmed,
                linkedSubject: period.subject,
                linkedPeriodId: period.id
            )
            newTaskTitle = ""
        }
    }

    // MARK: - Card Visual Styling
    @ViewBuilder
    private var cardBackground: some View {
        RoundedRectangle(cornerRadius: 10)
            .fill(Color(NSColor.controlBackgroundColor))
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(borderColor, lineWidth: status == .inProgress ? 1.5 : 1)
            )
            .overlay(
                // Subtle Fatimid Khatam watermark in corner
                KhatamEightPointStar()
                    .fill(FatimidPalette.watermark)
                    .frame(width: 44, height: 44)
                    .offset(x: 14, y: -14),
                alignment: .topTrailing
            )
            .clipShape(RoundedRectangle(cornerRadius: 10))
            .shadow(
                color: status == .inProgress ? FatimidPalette.emerald.opacity(0.15) : Color.black.opacity(0.03),
                radius: status == .inProgress ? 6 : 3,
                y: 1
            )
    }

    private var borderColor: Color {
        switch status {
        case .inProgress:
            return FatimidPalette.emerald.opacity(0.6)
        case .startingSoon:
            return Color.orange.opacity(0.4)
        case .changed:
            return Color.orange.opacity(0.35)
        case .completed:
            return Color.secondary.opacity(0.1)
        default:
            return Color.secondary.opacity(0.18)
        }
    }

    @ViewBuilder
    private var statusBadge: some View {
        switch status {
        case .inProgress:
            HStack(spacing: 3) {
                Circle()
                    .fill(FatimidPalette.emerald)
                    .frame(width: 6, height: 6)
                Text("IN SESSION")
                    .font(.system(size: 9, weight: .black))
                    .foregroundStyle(FatimidPalette.emerald)
            }
            .padding(.horizontal, 6)
            .padding(.vertical, 2)
            .background(Capsule().fill(FatimidPalette.emeraldSoft))

        case .startingSoon(let mins):
            HStack(spacing: 3) {
                Image(systemName: "clock.badge.exclamationmark")
                    .font(.system(size: 9))
                Text("IN \(mins)M")
                    .font(.system(size: 9, weight: .bold))
            }
            .foregroundStyle(.orange)
            .padding(.horizontal, 6)
            .padding(.vertical, 2)
            .background(Capsule().fill(Color.orange.opacity(0.15)))

        case .completed:
            HStack(spacing: 3) {
                Image(systemName: "checkmark")
                    .font(.system(size: 8, weight: .bold))
                Text("DONE")
                    .font(.system(size: 9, weight: .bold))
            }
            .foregroundStyle(.secondary)
            .padding(.horizontal, 5)
            .padding(.vertical, 2)
            .background(Capsule().fill(Color.secondary.opacity(0.1)))

        case .changed:
            Text("CHANGED")
                .font(.system(size: 9, weight: .bold))
                .foregroundStyle(.orange)
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Capsule().fill(Color.orange.opacity(0.15)))

        case .cancelled:
            Text("CANCELLED")
                .font(.system(size: 9, weight: .bold))
                .foregroundStyle(.red)
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Capsule().fill(Color.red.opacity(0.15)))

        case .upcoming:
            EmptyView()
        }
    }
}

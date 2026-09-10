import SwiftUI
import JadwalCore

public struct DateTimeRailView: View {
    @EnvironmentObject var env: AppEnvironment

    public let todayPeriods: [PeriodOccurrence]
    public let currentDayOfWeek: DayOfWeek

    @State private var currentTime = Date()
    private let timer = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

    public init(todayPeriods: [PeriodOccurrence], currentDayOfWeek: DayOfWeek) {
        self.todayPeriods = todayPeriods
        self.currentDayOfWeek = currentDayOfWeek
    }

    private var dayNumberString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "d"
        return formatter.string(from: currentTime)
    }

    private var monthYearString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "MMMM yyyy"
        return formatter.string(from: currentTime)
    }

    private var digitalClockString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        return formatter.string(from: currentTime)
    }

    private var completedCount: Int {
        todayPeriods.filter {
            ClassLiveStatus.compute(for: $0, currentTime: currentTime) == .completed
        }.count
    }

    private var inSessionPeriod: PeriodOccurrence? {
        todayPeriods.first {
            ClassLiveStatus.compute(for: $0, currentTime: currentTime) == .inProgress
        }
    }

    private var todayTaskCount: Int {
        let subjectSet = Set(todayPeriods.map(\.subject))
        let periodIdSet = Set(todayPeriods.map(\.id))
        return env.tasks.filter {
            !$0.isCompleted && (
                periodIdSet.contains($0.linkedPeriodId ?? "") ||
                subjectSet.contains($0.linkedSubject ?? "")
            )
        }.count
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            // Big Day + Month + Arabic Day
            HStack(alignment: .top, spacing: 12) {
                // Giant Day Number
                Text(dayNumberString)
                    .font(.system(size: 48, weight: .black, design: .rounded))
                    .foregroundStyle(.primary)
                    .lineLimit(1)

                VStack(alignment: .leading, spacing: 2) {
                    Text(currentDayOfWeek.rawValue.uppercased())
                        .font(.system(size: 13, weight: .bold, design: .monospaced))
                        .foregroundStyle(FatimidPalette.emerald)
                        .tracking(0.8)

                    Text(monthYearString)
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(.secondary)

                    Text(currentDayOfWeek.arabicName)
                        .font(.kanzalLulu(size: 15))
                        .foregroundStyle(FatimidPalette.bronze)
                }

                Spacer()

                // Live Clock Ticker
                VStack(alignment: .trailing, spacing: 2) {
                    Text(digitalClockString)
                        .font(.system(size: 14, weight: .bold, design: .monospaced))
                        .monospacedDigit()
                        .foregroundStyle(.primary)

                    HStack(spacing: 4) {
                        Circle()
                            .fill(FatimidPalette.emerald)
                            .frame(width: 5, height: 5)
                        Text("LIVE TIME")
                            .font(.system(size: 9, weight: .bold, design: .monospaced))
                            .foregroundStyle(.secondary)
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(
                    RoundedRectangle(cornerRadius: 6)
                        .fill(Color(NSColor.controlBackgroundColor))
                        .overlay(
                            RoundedRectangle(cornerRadius: 6)
                                .stroke(Color.secondary.opacity(0.15), lineWidth: 1)
                        )
                )
            }

            // Stats Row
            HStack(spacing: 8) {
                statPill(
                    icon: "book.closed.fill",
                    value: "\(todayPeriods.count)",
                    label: "Classes",
                    color: .primary
                )

                statPill(
                    icon: "checkmark.circle.fill",
                    value: "\(completedCount)",
                    label: "Done",
                    color: FatimidPalette.emerald
                )

                if let active = inSessionPeriod {
                    statPill(
                        icon: "record.circle",
                        value: active.subject,
                        label: "In Session",
                        color: FatimidPalette.emerald
                    )
                }

                if todayTaskCount > 0 {
                    statPill(
                        icon: "checklist",
                        value: "\(todayTaskCount)",
                        label: "Pending",
                        color: FatimidPalette.bronze
                    )
                }

                Spacer()

                if let snapshot = env.timetableSnapshot {
                    HStack(spacing: 4) {
                        Text(snapshot.academicYear)
                            .font(.system(size: 10, weight: .semibold, design: .monospaced))
                        if let week = snapshot.weekNumber {
                            Text("• W\(week)")
                                .font(.system(size: 10, weight: .bold))
                        }
                    }
                    .foregroundStyle(.secondary)
                    .padding(.horizontal, 7)
                    .padding(.vertical, 3)
                    .background(Capsule().fill(Color.secondary.opacity(0.08)))
                }
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(NSColor.controlBackgroundColor).opacity(0.75))
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color.secondary.opacity(0.12), lineWidth: 1)
                )
                .overlay(
                    // Subtle Khatam watermark
                    KhatamEightPointStar()
                        .fill(FatimidPalette.watermark)
                        .frame(width: 80, height: 80)
                        .offset(x: 20, y: -20),
                    alignment: .topTrailing
                )
                .clipShape(RoundedRectangle(cornerRadius: 12))
        )
        .onReceive(timer) { input in
            currentTime = input
        }
    }

    private func statPill(icon: String, value: String, label: String, color: Color) -> some View {
        HStack(spacing: 5) {
            Image(systemName: icon)
                .font(.system(size: 9))
                .foregroundStyle(color)

            Text(value)
                .font(value.containsArabic ? .kanzalLulu(size: 14) : .system(size: 11, weight: .bold, design: .rounded))
                .foregroundStyle(.primary)
                .lineLimit(1)

            Text(label.uppercased())
                .font(.system(size: 8, weight: .semibold, design: .monospaced))
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 7)
        .padding(.vertical, 4)
        .background(
            Capsule()
                .fill(Color(NSColor.textBackgroundColor).opacity(0.5))
        )
    }
}

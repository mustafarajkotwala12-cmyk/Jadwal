import SwiftUI
import JadwalCore

public struct TodayView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var currentTime: Date = Date()
    private let liveTicker = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

    public init() {}

    private var currentDayOfWeek: DayOfWeek {
        let weekday = Calendar.current.component(.weekday, from: currentTime)
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

    private var timelineItems: [ScheduleTimelineItem] {
        ScheduleTimelineBuilder.buildTimeline(from: todayPeriods, currentTime: currentTime)
    }

    private var scheduleRule: DayScheduleRule {
        DayScheduleRule.rule(for: currentDayOfWeek)
    }

    private var rowSplit: (row1: [ScheduleTimelineItem], row2: [ScheduleTimelineItem]) {
        ScheduleTimelineBuilder.splitIntoTwoHorizontalRows(items: timelineItems, day: currentDayOfWeek)
    }

    private var todayChanges: [TimetableChangeRecord] {
        todayPeriods.compactMap { $0.changeRecord }.filter { !$0.isAcknowledged }
    }

    public var body: some View {
        ScrollView(.vertical, showsIndicators: true) {
            VStack(alignment: .leading, spacing: 16) {
                // Freeform-inspired Date & Time Banner
                TimetableDateBanner(
                    dayOfWeek: currentDayOfWeek,
                    date: currentTime,
                    todayPeriods: todayPeriods,
                    rule: scheduleRule
                )

                // Schedule adjustments banner (if any active changes today)
                if !todayChanges.isEmpty {
                    changesBanner
                }

                // Main Two Horizontal Rows
                if todayPeriods.isEmpty {
                    emptyDayView
                } else {
                    // Row 1: Morning Sessions & Breaks
                    if !rowSplit.row1.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            rowHeader(
                                title: scheduleRule.row1Title,
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

                    // Row 2: Afternoon Sessions & Breaks
                    if !rowSplit.row2.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            rowHeader(
                                title: scheduleRule.row2Title,
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
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 16)
        }
        .onReceive(liveTicker) { date in
            currentTime = date
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

    // MARK: - Changes Banner
    private var changesBanner: some View {
        HStack(spacing: 10) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(.orange)

            VStack(alignment: .leading, spacing: 2) {
                Text("Schedule Adjustments Detected")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(.orange)

                Text(todayChanges.map(\.summaryMessage).joined(separator: " • "))
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            Button("Acknowledge") {
                Task {
                    await env.dismissAllChanges()
                }
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(Color.orange.opacity(0.1))
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .stroke(Color.orange.opacity(0.3), lineWidth: 1)
                )
        )
    }

    // MARK: - Empty Day View
    private var emptyDayView: some View {
        VStack(spacing: 12) {
            Spacer(minLength: 40)

            KhatamEightPointStar()
                .stroke(FatimidPalette.bronze.opacity(0.4), lineWidth: 1.5)
                .frame(width: 50, height: 50)

            Text("No Classes Scheduled Today")
                .font(.system(size: 18, weight: .bold, design: .rounded))
                .foregroundStyle(.primary)

            Text("Take this time for review, hifz revision, or personal study.")
                .font(.system(size: 13))
                .foregroundStyle(.secondary)

            Spacer(minLength: 40)
        }
        .frame(maxWidth: .infinity)
        .padding(30)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(NSColor.controlBackgroundColor).opacity(0.5))
        )
    }
}

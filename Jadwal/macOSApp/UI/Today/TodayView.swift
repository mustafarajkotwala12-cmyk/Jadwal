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

    private var columnSplit: (morning: [ScheduleTimelineItem], afternoon: [ScheduleTimelineItem]) {
        ScheduleTimelineBuilder.splitIntoTwoColumns(items: timelineItems)
    }

    private var todayChanges: [TimetableChangeRecord] {
        todayPeriods.compactMap { $0.changeRecord }.filter { !$0.isAcknowledged }
    }

    public var body: some View {
        GeometryReader { proxy in
            ScrollView(.vertical, showsIndicators: proxy.size.height < 680) {
                VStack(spacing: 12) {
                    // Header Date & Time Rail
                    DateTimeRailView(
                        todayPeriods: todayPeriods,
                        currentDayOfWeek: currentDayOfWeek
                    )

                    // Schedule adjustments banner (if any active changes today)
                    if !todayChanges.isEmpty {
                        changesBanner
                    }

                    // Main Two-Column Class Timetable
                    if todayPeriods.isEmpty {
                        emptyDayView
                    } else {
                        HStack(alignment: .top, spacing: 14) {
                            // Column 1: Morning Sessions
                            VStack(alignment: .leading, spacing: 8) {
                                columnHeader(
                                    title: "Morning Sessions",
                                    icon: "sun.and.horizon.fill",
                                    color: FatimidPalette.emerald
                                )

                                ForEach(columnSplit.morning) { item in
                                    renderTimelineItem(item)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .top)

                            // Column 2: Afternoon Sessions
                            VStack(alignment: .leading, spacing: 8) {
                                columnHeader(
                                    title: "Afternoon Sessions",
                                    icon: "sun.max.fill",
                                    color: FatimidPalette.bronze
                                )

                                ForEach(columnSplit.afternoon) { item in
                                    renderTimelineItem(item)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .top)
                        }
                    }
                }
                .padding(.horizontal, 18)
                .padding(.vertical, 14)
                .frame(minHeight: proxy.size.height, alignment: .top)
            }
        }
        .onReceive(liveTicker) { date in
            currentTime = date
        }
    }

    // MARK: - Column Header
    private func columnHeader(title: String, icon: String, color: Color) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 11, weight: .bold))
                .foregroundStyle(color)

            Text(title.uppercased())
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .foregroundStyle(.secondary)
                .tracking(0.6)

            Spacer()
        }
        .padding(.horizontal, 4)
        .padding(.top, 2)
    }

    // MARK: - Timeline Item Renderer
    @ViewBuilder
    private func renderTimelineItem(_ item: ScheduleTimelineItem) -> some View {
        switch item {
        case .classPeriod(let period, let status):
            ClassCardView(period: period, status: status)
        case .breakBlock(_, let name, let startTime, let endTime, let durationMinutes):
            BreakCardView(
                name: name,
                startTime: startTime,
                endTime: endTime,
                durationMinutes: durationMinutes
            )
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
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color.orange.opacity(0.1))
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
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

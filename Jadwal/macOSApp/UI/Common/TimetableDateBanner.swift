import SwiftUI
import JadwalCore

public struct TimetableDateBanner: View {
    public let dayOfWeek: DayOfWeek
    public let date: Date
    public let todayPeriods: [PeriodOccurrence]
    public let rule: DayScheduleRule

    public init(
        dayOfWeek: DayOfWeek,
        date: Date = Date(),
        todayPeriods: [PeriodOccurrence] = [],
        rule: DayScheduleRule? = nil
    ) {
        self.dayOfWeek = dayOfWeek
        self.date = date
        self.todayPeriods = todayPeriods
        self.rule = rule ?? DayScheduleRule.rule(for: dayOfWeek)
    }

    private var englishDateString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "EEEE, d MMMM yyyy"
        return formatter.string(from: date)
    }

    private var timeString: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "h:mm:ss a"
        return formatter.string(from: date)
    }

    private var activePeriod: PeriodOccurrence? {
        let statusList = todayPeriods.map { ($0, ClassLiveStatus.compute(for: $0, currentTime: date)) }
        return statusList.first(where: { $0.1 == .inProgress })?.0
    }

    private var nextPeriod: (PeriodOccurrence, Int)? {
        let statusList = todayPeriods.map { ($0, ClassLiveStatus.compute(for: $0, currentTime: date)) }
        for (period, status) in statusList {
            if case .startingSoon(let mins) = status {
                return (period, mins)
            }
        }
        return nil
    }

    private var arabicWeekdayName: String {
        switch dayOfWeek {
        case .monday: return "يوم الاثنين"
        case .tuesday: return "يوم الثلاثاء"
        case .wednesday: return "يوم الاربعاء"
        case .thursday: return "يوم الخميس"
        case .friday: return "يوم الجمعة"
        case .saturday: return "يوم السبت"
        case .sunday: return "يوم الأحد"
        }
    }

    public var body: some View {
        HStack(alignment: .center) {
            // LEFT: DATE & DAY INFO
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 8) {
                    Text(arabicWeekdayName)
                        .font(.kanzalLulu(size: 18))
                        .foregroundStyle(Color.white.opacity(0.95))

                    Text("•")
                        .foregroundStyle(Color.white.opacity(0.6))

                    Text(englishDateString)
                        .font(.system(size: 15, weight: .bold, design: .rounded))
                        .foregroundStyle(Color.white)
                }

                HStack(spacing: 6) {
                    Image(systemName: rule.isHalfDay ? "clock.badge.checkmark" : (rule.day == .friday ? "star.fill" : "calendar.badge.clock"))
                        .font(.system(size: 11))
                        .foregroundStyle(FatimidPalette.gold)

                    Text(rule.daySubtitle)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(Color.white.opacity(0.85))
                }
            }

            Spacer()

            // RIGHT: LIVE TIME & STATUS PILL
            VStack(alignment: .trailing, spacing: 5) {
                Text(timeString)
                    .font(.system(size: 24, weight: .heavy, design: .monospaced))
                    .foregroundStyle(Color.white)
                    .shadow(color: Color.black.opacity(0.15), radius: 2, x: 0, y: 1)

                // Live status indicator
                HStack(spacing: 6) {
                    if let active = activePeriod {
                        Circle()
                            .fill(Color.green)
                            .frame(width: 8, height: 8)
                        Text("Live: \(active.periodName) (\(active.subject))")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(Color.white.opacity(0.95))
                            .lineLimit(1)
                    } else if let (next, mins) = nextPeriod {
                        Circle()
                            .fill(Color.yellow)
                            .frame(width: 8, height: 8)
                        Text("Next: \(next.periodName) in \(mins)m")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(Color.white.opacity(0.95))
                    } else if todayPeriods.isEmpty {
                        Text("No classes today")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundStyle(Color.white.opacity(0.8))
                    } else {
                        Text("All classes concluded")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundStyle(Color.white.opacity(0.8))
                    }
                }
                .padding(.horizontal, 10)
                .padding(.vertical, 3)
                .background(
                    Capsule()
                        .fill(Color.black.opacity(0.2))
                )
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 14)
        .background(
            ZStack {
                // Freeform-inspired cheerful banner with Fatimid elegance
                LinearGradient(
                    colors: [
                        Color(red: 0.12, green: 0.53, blue: 0.78), // Vibrant cyan-blue
                        Color(red: 0.08, green: 0.38, blue: 0.62)  // Deep rich blue
                    ],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )

                // Delicate overlay motif
                HStack {
                    Spacer()
                    KhatamEightPointStar()
                        .fill(Color.white.opacity(0.08))
                        .frame(width: 90, height: 90)
                        .offset(x: 20, y: 0)
                }
            }
        )
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
        .shadow(color: Color(red: 0.12, green: 0.53, blue: 0.78).opacity(0.25), radius: 8, x: 0, y: 4)
    }
}

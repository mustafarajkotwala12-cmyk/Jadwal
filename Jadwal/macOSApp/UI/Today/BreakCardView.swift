import SwiftUI
import JadwalCore

public struct BreakCardView: View {
    public let name: String
    public let startTime: String
    public let endTime: String
    public let durationMinutes: Int

    public init(name: String, startTime: String, endTime: String, durationMinutes: Int) {
        self.name = name
        self.startTime = startTime
        self.endTime = endTime
        self.durationMinutes = durationMinutes
    }

    private var breakIcon: String {
        if name.localizedCaseInsensitiveContains("Lunch") || name.localizedCaseInsensitiveContains("Namaz") {
            return "sun.max.fill"
        } else if name.localizedCaseInsensitiveContains("Morning") {
            return "sun.horizon.fill"
        } else if name.localizedCaseInsensitiveContains("Recess") {
            return "cup.and.saucer.fill"
        } else {
            return "pause.circle.fill"
        }
    }

    public var body: some View {
        HStack(spacing: 8) {
            Image(systemName: breakIcon)
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(FatimidPalette.bronze)

            Text(name.uppercased())
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .foregroundStyle(.secondary)
                .tracking(0.6)

            Text("•")
                .font(.system(size: 9))
                .foregroundStyle(.tertiary)

            Text(ScheduleTimelineBuilder.formatDuration(minutes: durationMinutes))
                .font(.system(size: 10, weight: .semibold, design: .rounded))
                .foregroundStyle(.secondary)

            Spacer()

            Text("\(startTime) – \(endTime)")
                .font(.system(size: 10, weight: .medium, design: .monospaced))
                .foregroundStyle(.tertiary)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(NSColor.controlBackgroundColor).opacity(0.4))
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .strokeBorder(
                            style: StrokeStyle(lineWidth: 1, dash: [4, 4])
                        )
                        .foregroundStyle(Color.secondary.opacity(0.2))
                )
        )
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(name), \(startTime) to \(endTime), \(ScheduleTimelineBuilder.formatDuration(minutes: durationMinutes))")
    }
}

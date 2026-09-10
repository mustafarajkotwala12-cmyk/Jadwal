import SwiftUI
import JadwalCore

public struct VerticalBreakPillView: View {
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
            return "moon.stars.fill"
        } else if name.localizedCaseInsensitiveContains("Morning") {
            return "sun.horizon.fill"
        } else if name.localizedCaseInsensitiveContains("Recess") {
            return "cup.and.saucer.fill"
        } else {
            return "pause.circle.fill"
        }
    }

    private var shortDisplayName: String {
        if name.localizedCaseInsensitiveContains("Morning") {
            return "Morning\nPrep"
        } else if name.localizedCaseInsensitiveContains("Lunch") {
            return "Lunch &\nNamaz"
        } else if name.localizedCaseInsensitiveContains("Recess") {
            return "Recess"
        } else {
            return name
        }
    }

    public var body: some View {
        VStack(spacing: 8) {
            // Top: Icon
            ZStack {
                Circle()
                    .fill(FatimidPalette.gold.opacity(0.2))
                    .frame(width: 26, height: 26)
                Image(systemName: breakIcon)
                    .font(.system(size: 11, weight: .bold))
                    .foregroundStyle(FatimidPalette.bronze)
            }
            .padding(.top, 10)

            Spacer()

            // Middle: Vertical / Stacked Name
            Text(shortDisplayName)
                .font(.system(size: 11, weight: .bold, design: .rounded))
                .foregroundStyle(FatimidPalette.emerald)
                .multilineTextAlignment(.center)
                .lineSpacing(2)

            Spacer()

            // Bottom: Duration & Time Range
            VStack(spacing: 3) {
                Text(ScheduleTimelineBuilder.formatDuration(minutes: durationMinutes))
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(.primary)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(
                        Capsule()
                            .fill(Color.accentColor.opacity(0.15))
                    )

                Text("\(startTime)\n\(endTime)")
                    .font(.system(size: 9, weight: .medium, design: .monospaced))
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(.bottom, 10)
        }
        .frame(width: 54)
        .frame(maxHeight: .infinity)
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(NSColor.controlBackgroundColor).opacity(0.55))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .strokeBorder(
                            style: StrokeStyle(lineWidth: 1.2, dash: [4, 3])
                        )
                        .foregroundStyle(FatimidPalette.gold.opacity(0.35))
                )
        )
        .shadow(color: Color.black.opacity(0.04), radius: 3, x: 0, y: 1)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(name), \(startTime) to \(endTime), \(ScheduleTimelineBuilder.formatDuration(minutes: durationMinutes))")
    }
}

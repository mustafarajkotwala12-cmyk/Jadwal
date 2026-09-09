import SwiftUI
import SwiftCore

public struct PriorityBadge: View {
    public let priority: TaskPriority

    public init(priority: TaskPriority) {
        self.priority = priority
    }

    public var body: some View {
        Text(priority.rawValue)
            .font(.caption2.bold())
            .padding(.horizontal, 6)
            .padding(.vertical, 2)
            .background(
                Capsule()
                    .fill(badgeColor.opacity(0.15))
            )
            .foregroundStyle(badgeColor)
    }

    private var badgeColor: Color {
        switch priority {
        case .low: return .gray
        case .medium: return .blue
        case .high: return .orange
        case .urgent: return .red
        }
    }
}

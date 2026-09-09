import SwiftUI

public enum NavigationItem: String, CaseIterable, Identifiable {
    case today = "Today"
    case timetable = "Timetable"
    case tasks = "Tasks"
    case settings = "Settings"

    public var id: String { rawValue }

    public var iconName: String {
        switch self {
        case .today: return "calendar.badge.clock"
        case .timetable: return "tablecells"
        case .tasks: return "checklist"
        case .settings: return "gear"
        }
    }
}

public struct MainNavigationView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var selectedItem: NavigationItem? = .today

    public init() {}

    public var body: some View {
        NavigationSplitView {
            List(NavigationItem.allCases, selection: $selectedItem) { item in
                NavigationLink(value: item) {
                    Label(item.rawValue, systemImage: item.iconName)
                }
            }
            .navigationTitle("SWIFT")
            .listStyle(.sidebar)
        } detail: {
            switch selectedItem {
            case .today:
                TodayView()
            case .timetable:
                TimetableView()
            case .tasks:
                TasksView()
            case .settings:
                SettingsView()
            case .none:
                TodayView()
            }
        }
        .task {
            await env.loadData()
        }
    }
}

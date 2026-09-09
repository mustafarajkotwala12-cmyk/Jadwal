import SwiftUI

@main
struct SWIFTApp: App {
    @StateObject private var environment = AppEnvironment()

    var body: some Scene {
        WindowGroup {
            MainNavigationView()
                .environmentObject(environment)
                .frame(minWidth: 850, minHeight: 580)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)

        MenuBarExtra("SWIFT", systemImage: "calendar.badge.clock") {
            MenuBarView()
                .environmentObject(environment)
        }
        .menuBarExtraStyle(.window)
    }
}

import SwiftUI

@main
struct JadwalApp: App {
    @StateObject private var environment = AppEnvironment()

    init() {
        FontManager.registerFonts()
    }

    var body: some Scene {
        WindowGroup {
            MainNavigationView()
                .environmentObject(environment)
                .frame(minWidth: 850, minHeight: 580)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)

        MenuBarExtra("Jadwal", systemImage: "calendar.badge.clock") {
            MenuBarView()
                .environmentObject(environment)
        }
        .menuBarExtraStyle(.window)
    }
}

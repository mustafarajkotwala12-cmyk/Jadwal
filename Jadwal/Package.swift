// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Jadwal",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(name: "JadwalCore", targets: ["JadwalCore"]),
        .library(name: "JadwalPersistence", targets: ["JadwalPersistence"]),
        .library(name: "JadwalIntegrations", targets: ["JadwalIntegrations"]),
        .executable(name: "JadwalApp", targets: ["JadwalApp"]),
    ],
    dependencies: [],
    targets: [
        // 1. Core Domain and Application Use Cases (pure Swift, platform-neutral)
        .target(
            name: "JadwalCore",
            dependencies: [],
            path: "Packages/JadwalCore/Sources"
        ),
        .testTarget(
            name: "JadwalCoreTests",
            dependencies: ["JadwalCore"],
            path: "Packages/JadwalCore/Tests"
        ),

        // 2. Persistence Layer (Local storage / JSON / SQLite abstraction)
        .target(
            name: "JadwalPersistence",
            dependencies: ["JadwalCore"],
            path: "Packages/JadwalPersistence/Sources"
        ),

        // 3. Integrations (Jamea Helper, timetable importer)
        .target(
            name: "JadwalIntegrations",
            dependencies: ["JadwalCore"],
            path: "Packages/JadwalIntegrations/Sources"
        ),
        .testTarget(
            name: "JadwalIntegrationsTests",
            dependencies: ["JadwalIntegrations", "JadwalCore"],
            path: "Packages/JadwalIntegrations/Tests"
        ),

        // 4. Native macOS SwiftUI Shell
        .executableTarget(
            name: "JadwalApp",
            dependencies: [
                "JadwalCore",
                "JadwalPersistence",
                "JadwalIntegrations"
            ],
            path: "macOSApp",
            resources: [
                .process("Resources")
            ]
        ),
    ]
)

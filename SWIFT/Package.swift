// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SWIFT",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(name: "SwiftCore", targets: ["SwiftCore"]),
        .library(name: "SwiftPersistence", targets: ["SwiftPersistence"]),
        .library(name: "SwiftIntegrations", targets: ["SwiftIntegrations"]),
        .executable(name: "SWIFTApp", targets: ["SWIFTApp"]),
    ],
    dependencies: [],
    targets: [
        // 1. Core Domain and Application Use Cases (pure Swift, platform-neutral)
        .target(
            name: "SwiftCore",
            dependencies: [],
            path: "Packages/SwiftCore/Sources"
        ),
        .testTarget(
            name: "SwiftCoreTests",
            dependencies: ["SwiftCore"],
            path: "Packages/SwiftCore/Tests"
        ),

        // 2. Persistence Layer (Local storage / JSON / SQLite abstraction)
        .target(
            name: "SwiftPersistence",
            dependencies: ["SwiftCore"],
            path: "Packages/SwiftPersistence/Sources"
        ),

        // 3. Integrations (Jamea Helper, timetable importer)
        .target(
            name: "SwiftIntegrations",
            dependencies: ["SwiftCore"],
            path: "Packages/SwiftIntegrations/Sources"
        ),
        .testTarget(
            name: "SwiftIntegrationsTests",
            dependencies: ["SwiftIntegrations", "SwiftCore"],
            path: "Packages/SwiftIntegrations/Tests"
        ),

        // 4. Native macOS SwiftUI Shell
        .executableTarget(
            name: "SWIFTApp",
            dependencies: [
                "SwiftCore",
                "SwiftPersistence",
                "SwiftIntegrations"
            ],
            path: "macOSApp",
            resources: [
                .process("Resources")
            ]
        ),
    ]
)

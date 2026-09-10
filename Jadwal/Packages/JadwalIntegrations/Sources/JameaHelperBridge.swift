import Foundation
import JadwalCore

public enum HelperBridgeError: LocalizedError, Sendable {
    case scriptNotFound(String)
    case executionFailed(Int, String)
    case dataFileNotFound(String)

    public var errorDescription: String? {
        switch self {
        case .scriptNotFound(let path):
            return "Helper script not found at path: \(path)"
        case .executionFailed(let code, let message):
            return "Helper script failed with code \(code): \(message)"
        case .dataFileNotFound(let path):
            return "Timetable data file not found at: \(path)"
        }
    }
}

public final class JameaHelperBridge: Sendable {
    private let helperScriptPath: String
    private let dataFilePath: String
    private let workingDirectory: String
    private let pythonExecutable: String
    private let importer: TimetableImporter

    public static func resolveWorkspaceDirectory() -> String {
        let fileManager = FileManager.default

        // 0. Check inside App Bundle Resources
        if let resourceURL = Bundle.main.resourceURL {
            let bundleHelper = resourceURL.appendingPathComponent("helper/jamea_helper.py").path
            if fileManager.fileExists(atPath: bundleHelper) {
                return resourceURL.path
            }
        }

        // 1. Check environment variable
        if let envPath = ProcessInfo.processInfo.environment["JAMEA_HELPER_DIR"],
           fileManager.fileExists(atPath: (envPath as NSString).appendingPathComponent("helper/jamea_helper.py")) {
            return envPath
        }

        // 2. Check current working directory and walk upwards
        var searchDir = URL(fileURLWithPath: fileManager.currentDirectoryPath)
        for _ in 0..<5 {
            let candidate = searchDir.appendingPathComponent("helper/jamea_helper.py").path
            if fileManager.fileExists(atPath: candidate) {
                return searchDir.path
            }
            searchDir = searchDir.deletingLastPathComponent()
        }

        // 3. Check Bundle main bundleURL / executable URL and walk upwards
        var bundleSearchDir = Bundle.main.bundleURL
        for _ in 0..<6 {
            let candidate = bundleSearchDir.appendingPathComponent("helper/jamea_helper.py").path
            if fileManager.fileExists(atPath: candidate) {
                return bundleSearchDir.path
            }
            bundleSearchDir = bundleSearchDir.deletingLastPathComponent()
        }

        // 4. Check known default workspace locations
        let knownPaths = [
            "/Users/mustafarajkotwala/JameaHelper",
            fileManager.homeDirectoryForCurrentUser.appendingPathComponent("JameaHelper").path
        ]
        for path in knownPaths {
            if fileManager.fileExists(atPath: (path as NSString).appendingPathComponent("helper/jamea_helper.py")) {
                return path
            }
        }

        return fileManager.currentDirectoryPath
    }

    public static func resolvePythonExecutable() -> String {
        let candidates = [
            "/Library/Frameworks/Python.framework/Versions/3.13/bin/python3",
            "/Library/Frameworks/Python.framework/Versions/3.12/bin/python3",
            "/Library/Frameworks/Python.framework/Versions/3.11/bin/python3",
            "/opt/homebrew/bin/python3",
            "/usr/local/bin/python3",
            "/usr/bin/python3"
        ]
        for candidate in candidates {
            if FileManager.default.fileExists(atPath: candidate) {
                return candidate
            }
        }
        return "/usr/bin/python3"
    }

    public init(
        workingDirectory: String? = nil,
        pythonExecutable: String? = nil,
        importer: TimetableImporter = TimetableImporter()
    ) {
        let base = workingDirectory ?? Self.resolveWorkspaceDirectory()
        self.workingDirectory = base
        self.pythonExecutable = pythonExecutable ?? Self.resolvePythonExecutable()
        self.helperScriptPath = (base as NSString).appendingPathComponent("helper/jamea_helper.py")
        self.dataFilePath = (base as NSString).appendingPathComponent("data/timetable.json")
        self.importer = importer
    }

    /// Directly reads and parses data/timetable.json if it exists.
    public func loadCurrentSnapshot() throws -> TimetableSnapshot {
        var path = dataFilePath
        if !FileManager.default.fileExists(atPath: path) {
            let jadwalAppSupport = FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library/Application Support/Jadwal/data/timetable.json").path
            let legacyAppSupport = FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library/Application Support/SWIFT/data/timetable.json").path

            if FileManager.default.fileExists(atPath: jadwalAppSupport) {
                path = jadwalAppSupport
            } else if FileManager.default.fileExists(atPath: legacyAppSupport) {
                path = legacyAppSupport
            } else {
                throw HelperBridgeError.dataFileNotFound(dataFilePath)
            }
        }
        let url = URL(fileURLWithPath: path)
        return try importer.importFromFile(at: url)
    }

    /// Runs python3 helper/jamea_helper.py and returns the updated timetable snapshot.
    public func executeHelperAndReload(forceLogin: Bool = false) async throws -> TimetableSnapshot {
        guard FileManager.default.fileExists(atPath: helperScriptPath) else {
            throw HelperBridgeError.scriptNotFound(helperScriptPath)
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: pythonExecutable)
        process.arguments = forceLogin ? [helperScriptPath, "--login"] : [helperScriptPath]
        process.currentDirectoryURL = URL(fileURLWithPath: workingDirectory)

        var environment = ProcessInfo.processInfo.environment
        let pythonBinDir = (pythonExecutable as NSString).deletingLastPathComponent
        let extraPaths = "\(pythonBinDir):/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin"
        if let existingPath = environment["PATH"] {
            environment["PATH"] = "\(extraPaths):\(existingPath)"
        } else {
            environment["PATH"] = extraPaths
        }

        // If running inside app bundle resources, route data directory to Application Support
        if (workingDirectory as NSString).contains(".app/Contents/Resources") {
            let appSupportDataDir = FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library/Application Support/Jadwal/data")
            try? FileManager.default.createDirectory(at: appSupportDataDir, withIntermediateDirectories: true)
            environment["JAMEA_DATA_DIR"] = appSupportDataDir.path
        }

        process.environment = environment

        let outputPipe = Pipe()
        let errorPipe = Pipe()
        process.standardOutput = outputPipe
        process.standardError = errorPipe

        try process.run()
        process.waitUntilExit()

        let exitCode = Int(process.terminationStatus)
        if exitCode != 0 {
            let errorData = errorPipe.fileHandleForReading.readDataToEndOfFile()
            let errorMsg = String(data: errorData, encoding: .utf8) ?? "Unknown execution error"
            throw HelperBridgeError.executionFailed(exitCode, errorMsg)
        }

        return try loadCurrentSnapshot()
    }
}

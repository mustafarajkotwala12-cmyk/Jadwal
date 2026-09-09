import Foundation
import SwiftUI
import SwiftCore
import SwiftPersistence
import SwiftIntegrations

public enum DiscrepancyResolutionAction: Sendable {
    case adoptNewSubject
    case keepOriginalContext
    case dismiss
}

@MainActor
public final class AppEnvironment: ObservableObject {
    @Published public var timetableSnapshot: TimetableSnapshot?
    @Published public var tasks: [TaskItem] = []
    @Published public var activeChanges: [TimetableChangeRecord] = []
    @Published public var isSyncing: Bool = false
    @Published public var statusMessage: String?
    @Published public var errorMessage: String?

    public let timetableService: TimetableService
    public let taskService: TaskService
    public let changeRepository: LocalFileChangeRepository
    public let diffEngine = TimetableDiffEngine()
    public let helperBridge: JameaHelperBridge

    public init() {
        // App Support or local data directory
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let swiftDataDir = appSupport.appendingPathComponent("SWIFT", isDirectory: true)

        let timetableRepo = LocalFileTimetableRepository(storageDirectory: swiftDataDir)
        let taskRepo = LocalFileTaskRepository(storageDirectory: swiftDataDir)
        self.changeRepository = LocalFileChangeRepository(storageDirectory: swiftDataDir)

        self.timetableService = TimetableService(repository: timetableRepo)
        self.taskService = TaskService(repository: taskRepo)

        // Initialize Jamea Helper bridge with automatic workspace & Python discovery
        self.helperBridge = JameaHelperBridge()
    }

    public func loadData() async {
        do {
            // Load stored tasks
            self.tasks = try await taskService.getAllTasks()

            // Load stored changes (only unacknowledged ones are active)
            let stored = (try? await changeRepository.fetchChanges()) ?? []
            self.activeChanges = stored.filter { !$0.isAcknowledged }

            // Try loading stored timetable, or fallback to current helper snapshot
            var loadedSnapshot: TimetableSnapshot?
            if let saved = try await timetableService.getLatestTimetable() {
                loadedSnapshot = saved
            } else if let live = try? helperBridge.loadCurrentSnapshot() {
                loadedSnapshot = live
                try? await timetableService.saveTimetable(live)
            }

            if let snapshot = loadedSnapshot {
                self.timetableSnapshot = diffEngine.annotateSnapshot(snapshot, with: activeChanges)
            }
        } catch {
            self.errorMessage = "Failed to load data: \(error.localizedDescription)"
        }
    }

    public func refreshTimetable() async {
        isSyncing = true
        statusMessage = "Syncing timetable with Jamea Helper..."
        errorMessage = nil

        do {
            let updatedRaw = try await helperBridge.executeHelperAndReload()
            
            // Diff against old snapshot
            let newChanges = diffEngine.diff(oldSnapshot: self.timetableSnapshot, newSnapshot: updatedRaw)
            
            if !newChanges.isEmpty {
                try await changeRepository.appendChanges(newChanges)
                let allChanges = (try? await changeRepository.fetchChanges()) ?? newChanges
                self.activeChanges = allChanges.filter { !$0.isAcknowledged }
                
                // Task protection analysis: Check if existing tasks belong to modified periods
                for i in 0..<tasks.count {
                    guard let periodId = tasks[i].linkedPeriodId else { continue }
                    if let matching = newChanges.first(where: { $0.periodId == periodId }) {
                        if matching.changeType == .subjectChanged, let newSub = matching.newSubject {
                            let originalSub = tasks[i].linkedSubject ?? matching.oldSubject ?? "Unknown"
                            tasks[i].discrepancy = TaskDiscrepancy(
                                originalSubject: originalSub,
                                newSubject: newSub,
                                periodId: periodId
                            )
                            try? await taskService.updateTask(tasks[i])
                        } else if matching.changeType == .removed {
                            let originalSub = tasks[i].linkedSubject ?? matching.oldSubject ?? "Unknown"
                            tasks[i].discrepancy = TaskDiscrepancy(
                                originalSubject: originalSub,
                                newSubject: "Cancelled / Removed",
                                periodId: periodId
                            )
                            try? await taskService.updateTask(tasks[i])
                        }
                    }
                }
            } else {
                let allChanges = (try? await changeRepository.fetchChanges()) ?? []
                self.activeChanges = allChanges.filter { !$0.isAcknowledged }
            }

            // Annotate snapshot with active changes so UI cards display badges only for current updates
            let annotated = diffEngine.annotateSnapshot(updatedRaw, with: self.activeChanges)
            try await timetableService.saveTimetable(annotated)
            self.timetableSnapshot = annotated
            
            if newChanges.isEmpty {
                statusMessage = "Timetable synced (no changes detected)."
            } else {
                statusMessage = "Timetable updated! \(newChanges.count) change(s) detected."
            }
        } catch {
            errorMessage = "Sync failed: \(error.localizedDescription)"
        }

        isSyncing = false
    }

    public func dismissAllChanges() async {
        try? await changeRepository.acknowledgeAllChanges()
        self.activeChanges = []
        if let snapshot = self.timetableSnapshot {
            let cleared = diffEngine.annotateSnapshot(snapshot, with: [])
            try? await timetableService.saveTimetable(cleared)
            self.timetableSnapshot = cleared
        }
    }

    public func resolveTaskDiscrepancy(taskId: UUID, action: DiscrepancyResolutionAction) async {
        guard let idx = tasks.firstIndex(where: { $0.id == taskId }) else { return }
        switch action {
        case .adoptNewSubject:
            tasks[idx].adoptNewSubject()
        case .keepOriginalContext:
            tasks[idx].keepOriginalContext()
        case .dismiss:
            tasks[idx].dismissDiscrepancy()
        }
        do {
            try await taskService.updateTask(tasks[idx])
        } catch {
            self.errorMessage = "Failed to update task: \(error.localizedDescription)"
        }
    }

    public func addTask(title: String, notes: String? = nil, priority: TaskPriority = .medium, linkedSubject: String? = nil, linkedPeriodId: String? = nil) async {
        do {
            let task = try await taskService.createTask(
                title: title,
                notes: notes,
                priority: priority,
                linkedSubject: linkedSubject,
                linkedPeriodId: linkedPeriodId
            )
            self.tasks.append(task)
        } catch {
            self.errorMessage = "Failed to add task: \(error.localizedDescription)"
        }
    }

    public func toggleTask(_ task: TaskItem) async {
        do {
            let updated = try await taskService.toggleTaskCompletion(task: task)
            if let idx = tasks.firstIndex(where: { $0.id == updated.id }) {
                tasks[idx] = updated
            }
        } catch {
            self.errorMessage = "Failed to update task: \(error.localizedDescription)"
        }
    }

    public func deleteTask(id: UUID) async {
        do {
            try await taskService.deleteTask(id: id)
            tasks.removeAll(where: { $0.id == id })
        } catch {
            self.errorMessage = "Failed to delete task: \(error.localizedDescription)"
        }
    }
}

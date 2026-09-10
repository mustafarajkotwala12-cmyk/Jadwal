import SwiftUI
import JadwalCore

public struct TasksView: View {
    @EnvironmentObject var env: AppEnvironment
    @State private var showingAddSheet: Bool = false
    @State private var filter: TaskFilter = .pending
    @State private var newTaskTitle: String = ""
    @State private var newTaskPriority: TaskPriority = .medium
    @State private var newTaskSubject: String = ""

    enum TaskFilter: String, CaseIterable {
        case pending = "Pending"
        case completed = "Completed"
        case all = "All"
    }

    public init() {}

    private var filteredTasks: [TaskItem] {
        switch filter {
        case .pending:
            return env.tasks.filter { !$0.isCompleted }
        case .completed:
            return env.tasks.filter { $0.isCompleted }
        case .all:
            return env.tasks
        }
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Header Bar
            HStack {
                Picker("Filter", selection: $filter) {
                    ForEach(TaskFilter.allCases, id: \.self) { f in
                        Text(f.rawValue).tag(f)
                    }
                }
                .pickerStyle(.segmented)
                .frame(width: 250)

                Spacer()

                Button(action: { showingAddSheet = true }) {
                    Label("Add Task", systemImage: "plus")
                }
                .buttonStyle(.borderedProminent)
            }
            .padding()
            .background(Color(NSColor.windowBackgroundColor))

            Divider()

            if filteredTasks.isEmpty {
                ContentUnavailableView(
                    "No Tasks",
                    systemImage: "checkmark.circle",
                    description: Text("No tasks in the \(filter.rawValue.lowercased()) filter.")
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                List {
                    ForEach(filteredTasks) { task in
                        VStack(alignment: .leading, spacing: 6) {
                            HStack(spacing: 12) {
                                Button(action: {
                                    Task { await env.toggleTask(task) }
                                }) {
                                    Image(systemName: task.isCompleted ? "checkmark.circle.fill" : "circle")
                                        .font(.title3)
                                        .foregroundStyle(task.isCompleted ? .green : .secondary)
                                }
                                .buttonStyle(.plain)

                                VStack(alignment: .leading, spacing: 3) {
                                    Text(task.title)
                                        .strikethrough(task.isCompleted)
                                        .foregroundStyle(task.isCompleted ? .secondary : .primary)

                                    if let subject = task.linkedSubject, !subject.isEmpty {
                                        Label(subject, systemImage: "book.pages")
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                    }
                                }

                                Spacer()

                                PriorityBadge(priority: task.priority)

                                Button(role: .destructive, action: {
                                    Task { await env.deleteTask(id: task.id) }
                                }) {
                                    Image(systemName: "trash")
                                        .foregroundStyle(.secondary)
                                }
                                .buttonStyle(.plain)
                            }

                            // Discrepancy protection banner
                            if let disc = task.discrepancy, !disc.isDismissed {
                                HStack(alignment: .center, spacing: 10) {
                                    Image(systemName: "exclamationmark.triangle.fill")
                                        .foregroundStyle(.orange)

                                    VStack(alignment: .leading, spacing: 2) {
                                        Text("Timetable Changed: Subject changed from \"\(disc.originalSubject)\" to \"\(disc.newSubject)\"")
                                            .font(.caption.bold())
                                            .foregroundStyle(.orange)
                                        Text("Original task context was preserved. Choose how you want to update it:")
                                            .font(.caption2)
                                            .foregroundStyle(.secondary)
                                    }

                                    Spacer()

                                    HStack(spacing: 6) {
                                        Button("Adopt '\(disc.newSubject)'") {
                                            Task { await env.resolveTaskDiscrepancy(taskId: task.id, action: .adoptNewSubject) }
                                        }
                                        .font(.caption2.bold())
                                        .buttonStyle(.borderedProminent)
                                        .controlSize(.small)

                                        Button("Keep '\(disc.originalSubject)'") {
                                            Task { await env.resolveTaskDiscrepancy(taskId: task.id, action: .keepOriginalContext) }
                                        }
                                        .font(.caption2)
                                        .buttonStyle(.bordered)
                                        .controlSize(.small)

                                        Button("Dismiss") {
                                            Task { await env.resolveTaskDiscrepancy(taskId: task.id, action: .dismiss) }
                                        }
                                        .font(.caption2)
                                        .buttonStyle(.borderless)
                                        .controlSize(.small)
                                    }
                                }
                                .padding(8)
                                .background(
                                    RoundedRectangle(cornerRadius: 8)
                                        .fill(Color.orange.opacity(0.1))
                                        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.orange.opacity(0.3), lineWidth: 1))
                                )
                            }
                        }
                        .padding(.vertical, 4)
                    }
                }
                .listStyle(.inset)
            }
        }
        .navigationTitle("Tasks")
        .sheet(isPresented: $showingAddSheet) {
            VStack(alignment: .leading, spacing: 16) {
                Text("New Task")
                    .font(.headline)

                TextField("Task title...", text: $newTaskTitle)
                    .textFieldStyle(.roundedBorder)

                Picker("Priority", selection: $newTaskPriority) {
                    ForEach(TaskPriority.allCases, id: \.self) { p in
                        Text(p.rawValue).tag(p)
                    }
                }

                if let subjects = env.timetableSnapshot?.uniqueSubjects, !subjects.isEmpty {
                    Picker("Link to Subject", selection: $newTaskSubject) {
                        Text("None").tag("")
                        ForEach(subjects, id: \.self) { subj in
                            Text(subj).tag(subj)
                        }
                    }
                }

                HStack {
                    Button("Cancel") { showingAddSheet = false }
                    Spacer()
                    Button("Create") {
                        guard !newTaskTitle.trimmingCharacters(in: .whitespaces).isEmpty else { return }
                        let subj = newTaskSubject.isEmpty ? nil : newTaskSubject
                        Task {
                            await env.addTask(
                                title: newTaskTitle,
                                priority: newTaskPriority,
                                linkedSubject: subj
                            )
                            newTaskTitle = ""
                            newTaskSubject = ""
                            showingAddSheet = false
                        }
                    }
                    .buttonStyle(.borderedProminent)
                }
            }
            .padding()
            .frame(width: 380)
        }
    }
}

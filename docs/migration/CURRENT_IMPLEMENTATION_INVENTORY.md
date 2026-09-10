# Current Implementation Inventory: Jadwal Desktop

**Date**: 2026-09-10  
**Project**: Jadwal (Formerly JameaHelper / SWIFT)  
**Scope**: 100% Comprehensive inventory of all source files, frameworks, configurations, assets, and dependencies.

---

## 1. Swift Workspace (`Jadwal/`)

### 1.1 Package Manifest & Configuration
- `Jadwal/Package.swift`: SwiftPM manifest configuring targets:
  - `JadwalCore` (Pure library target)
  - `JadwalPersistence` (File-based storage actor implementations)
  - `JadwalIntegrations` (Timetable parser & Python bridge)
  - `JadwalApp` (macOS executable target with SwiftUI / AppKit)
  - `JadwalCoreTests`, `JadwalIntegrationsTests` (Unit test targets)
- `Jadwal/scripts/bundle_app.sh`: Script packaging binary into `Jadwal.app` bundle and creating distribution `.zip`.
- `Jadwal/FEATURES.md`: Product roadmap and technical implementation notes.

### 1.2 Packages (`Jadwal/Packages/`)
#### `JadwalCore`
- `Sources/Domain/Timetable.swift`:
  - `DayOfWeek` (enum: monday..sunday with localized display and Arabic weekday names)
  - `PeriodOccurrence` (struct: day, dateString, periodName, startTime, endTime, subject, details, changeRecord)
  - `TimetableSnapshot` (struct: academicYear, weekNumber, periods)
  - `TimetableChangeRecord` (struct: changeType, periodId, old/new subject, times, teacher, room, isAcknowledged)
  - `ChangeType` (enum: added, removed, timeChanged, subjectChanged, teacherChanged, roomChanged, cancelled)
- `Sources/Domain/Task.swift`:
  - `TaskItem` (struct: id, title, notes, priority, category, isCompleted, createdAt, completedAt, linkedSubject, linkedPeriodId, discrepancy)
  - `TaskPriority` (enum: low, medium, high, urgent)
  - `TaskCategory` (enum: academic, revision, hifz, homework, general)
  - `TaskDiscrepancy` (struct: originalSubject, newSubject, periodId, isDismissed)
- `Sources/Domain/ScheduleTimeline.swift`:
  - `ScheduleTimelineItem` (enum: classPeriod, breakBlock)
  - `ClassLiveStatus` (enum: upcoming, startingSoon(minutes), inProgress, completed, changed, cancelled)
  - `DayScheduleRule` (struct: rule(for: DayOfWeek), handling Mon-Thu, Friday, and Saturday exceptions)
  - `ScheduleTimelineBuilder` (builder detecting gaps >= 10 min, naming breaks, and splitting into two horizontal rows)
- `Sources/Domain/TimetableDiffEngine.swift`:
  - Diff calculation comparing old and new timetable snapshots; non-destructive annotation.
- `Sources/Contracts/Repositories.swift`:
  - Protocols: `TaskRepository`, `TimetableRepository`, `SettingsRepository`.
- `Sources/Application/TaskService.swift`:
  - CRUD operations on tasks, toggle completion, discrepancy updates.
- `Sources/Application/TimetableService.swift`:
  - Snapshot retrieval and storage.
- `Tests/JadwalCoreTests.swift`:
  - Unit tests covering period IDs, diff engine, task integrity, discrepancy resolution, timeline breaks, day schedule rules, and horizontal splitting.

#### `JadwalPersistence`
- `Sources/FileRepositories.swift`:
  - `LocalFileTimetableRepository`: Atomic JSON read/write for `stored_timetable.json`.
  - `LocalFileTaskRepository`: Atomic JSON read/write for `stored_tasks.json`.
  - `LocalFileChangeRepository`: Atomic JSON read/write for `stored_changes.json`.

#### `JadwalIntegrations`
- `Sources/TimetableImporter.swift`:
  - JSON parser converting raw dictionary payloads into normalized `TimetableSnapshot` instances.
- `Sources/JameaHelperBridge.swift`:
  - Process runner calling `helper/jamea_helper.py` or fallback reading `data/timetable.json`.
- `Tests/TimetableImporterTests.swift`:
  - Integration tests verifying parsing of real timetable JSON files from disk.

### 1.3 macOS Application UI (`Jadwal/macOSApp/`)
- `JadwalApp.swift`: SwiftUI App entrypoint with `WindowGroup` and `MenuBarExtra` system tray companion.
- `AppEnvironment.swift`: Central `@MainActor` environment coordinating state, services, diffing, and tasks.
- `UI/MainNavigationView.swift`: Sidebar navigation with Today, Timetable, Tasks, Settings.
- `UI/Today/TodayView.swift`: Freeform-inspired dashboard with `TimetableDateBanner` and 2 horizontal rows of cards + break pills.
- `UI/Today/ClassCardView.swift`: Interactive 3D flip card with subject (large Kanz-al-Lulu typography), live badge, ustaadh, and flip face for task tracking.
- `UI/Today/VerticalBreakPillView.swift`: Narrow vertical break pill showing icon, vertical name, duration pill, and start/end times.
- `UI/Today/BreakCardView.swift`: Standard horizontal break card.
- `UI/Today/DateTimeRailView.swift`: Alternative compact date-time widget.
- `UI/Timetable/TimetableView.swift`: Day picker (Mon–Sat) + `TimetableDateBanner` + 2 horizontal card rows with add-task sheet.
- `UI/Tasks/TasksView.swift`: Task filter view (All, Pending, Completed), category tags, priority pills, and creation dialog.
- `UI/MenuBar/MenuBarView.swift`: Quick status panel with next/current class, quick task entry, and top pending tasks.
- `UI/Settings/SettingsView.swift`: Sync control, ITS token status, and preferences.
- `UI/Common/TimetableDateBanner.swift`: Full-width banner showing English date, Arabic weekday, day schedule exception subtitle, ticking clock, and active period pill.
- `UI/Common/ArabicFont.swift`: Kanz-al-Lulu custom font loader and helper extensions.
- `UI/Common/FatimidMotif.swift`: Vector geometric ornaments: `KhatamEightPointStar`, `FatimidArchDivider`, `FatimidPalette`.
- `Resources/KanzalLulu-Regular.ttf`: Embedded Arabic calligraphy typeface.
- `Resources/AppIcon.icns`: Application icon asset.

---

## 2. Python Integration (`helper/`)

- `helper/jamea_helper.py`:
  - 908 lines of Python.
  - Dependencies: `playwright`, `openpyxl`, `pandas`.
  - Responsibilities:
    1. Reads/writes JWT tokens to `jamea_token.json`.
    2. Validates JWT signature and expiration (`exp`).
    3. Launches Playwright headless browser for authentication to `https://beta.jameasaifiyah.org/`.
    4. Downloads weekly timetable Excel/API payload.
    5. Normalizes days, periods, times, subjects, and rooms into `Data/timetable.json`.
- `requirements.txt`:
  - `playwright>=1.40.0`
  - `openpyxl>=3.1.0`
  - `pandas>=2.0.0`
- `setup.sh`:
  - Shell setup creating python virtual environment and installing Playwright browser binaries.

---

## 3. Data Storage & Formats (`Data/`)

- `Data/timetable.json`:
  - Standard timetable format:
    ```json
    {
      "academicYear": "1447-1448",
      "weekNumber": 25,
      "generatedAt": "2026-09-08T08:00:00",
      "periods": [
        {
          "day": "Monday",
          "date": "2026-09-07",
          "period": "Period 2",
          "startTime": "08:50",
          "endTime": "09:25",
          "subject": "الرسالة الشريفة (الف)",
          "details": "...الشيخ ابراهيم بهائي الشيخ علي"
        }
      ]
    }
    ```
- `stored_tasks.json`:
  - Array of `TaskItem` serialized as JSON.
- `stored_changes.json`:
  - Array of `TimetableChangeRecord` serialized as JSON.
- `jamea_token.json`:
  - Single object `{ "access_token": "..." }` with file permissions `0600`.

# SWIFT Feature Changelog & Architecture Inventory

This document tracks all features, capabilities, and system components incorporated into the **SWIFT** native macOS application and the companion **Jamea Helper** pipeline.

---

## System Overview
- **App Name**: SWIFT (Student Workflow & Intelligent Foundation Tracker)
- **Platforms**: macOS 14.0+ (Sonoma, Sequoia) Native Swift/SwiftUI + Python Playwright Pipeline
- **Workspace Architecture**: Swift Package Manager modular framework with local persistent file backing.

---

## Feature Matrix

### 1. Timetable Ingestion & Authentication
- **Playwright ITS Authentication (`helper/jamea_helper.py`)**:
  - Secure local browser context persistence (`browser_profile`).
  - Web authentication token capture and restoration directly into web browser `sessionStorage` via `add_init_script()`.
  - Automatic `401 AUTH_EXPIRED` detection, cached token clearance, and re-login flow without manual intervention.
- **Excel Schedule Parser**:
  - Arabic day header normalization (`يوم الاثنين` → `Monday`, etc.).
  - Header extraction (Academic Year, Week Number, Date Range).
  - Cell regex parsing for slot timings (`HH:mm - HH:mm`).
  - Clean export to standardized JSON schema (`data/timetable.json`) with 57 weekly period slots.
  - Quiet command-line mode to avoid noisy standard output in production runs.

### 2. Timetable Importer & Bridge (`SwiftIntegrations`)
- **`TimetableImporter`**:
  - Decodes JSON payload with Arabic/English period models, slot times, and class locations into domain entities.
- **`JameaHelperBridge`**:
  - Seamless child-process execution (`python3 helper/jamea_helper.py`) from within Swift using `Process` and `Pipe`.
  - Reloads freshly parsed snapshots directly into app memory.

### 3. Change Management & Task Protection Engine (`SwiftCore`)
- **Slot-Based Deterministic Period Identity**:
  - Stable period ID format: `"{dateString}_{periodName}"` (e.g. `2026-09-07_Period_2`).
  - Period identity remains constant across timetable revisions even if subject, teacher, or time shifts.
- **`TimetableDiffEngine`**:
  - Classifies snapshot discrepancies into granular change types:
    - `SUBJECT_CHANGED`: Period subject reallocated (e.g., *Linguistics* → *Economics*).
    - `TIME_CHANGED`: Start or end timing adjusted.
    - `DETAILS_CHANGED`: Room or instructor updated.
    - `ADDED`: Brand new period slot scheduled.
    - `REMOVED`: Class slot cancelled or dropped.
  - Generates audit records (`TimetableChangeRecord`) stamped with timestamps and change summaries.
- **Non-Destructive Task Protection (`TaskDiscrepancy`)**:
  - **Zero Silent Data Loss**: Timetable subject changes never overwrite or reassign user tasks without student confirmation.
  - Detects tasks attached to altered timetable slots and flags them with a `TaskDiscrepancy` record.
  - Student resolution workflows:
    - **Adopt New Subject**: One-click updates task to the newly assigned subject.
    - **Keep Original Context**: Retains original subject and task notes while clearing the warning.
    - **Dismiss**: Clears the banner while keeping original task properties intact.

### 4. macOS Native User Interface (`macOSApp`)
- **Today Class Timetable (`TodayView`, `ClassCardView`, `BreakCardView`, `DateTimeRailView`)**:
  - **No-Scroll Guarantee**: Two-column responsive card grid (Morning Sessions & Afternoon Sessions) designed to fit all 8–10 daily periods and breaks on standard macOS window heights without vertical scrolling.
  - **Dominant Subject Typography**: Large, bold, scannable subject font readable from a distance.
  - **Live Class Status Engine**: Automatically computes dynamic state (`Completed`, `In Progress` with emerald pulse indicator, `Starting Soon` with amber countdown pill, `Upcoming`, `Cancelled`, `Changed`).
  - **Intelligent Chronological Break Blocks**: Context-aware gap calculation inserting minimal dashed break blocks (*Morning Preparation*, *Recess*, *Lunch & Namaz Break*).
  - **3D Vertical-Axis Card Flip**: Flipping any card reveals linked tasks for that specific subject and period occurrence, with checkboxes for rapid completion and an inline quick `+ Add Task` field.
  - **Accessibility**: Full keyboard navigation (Space/Return to flip) and instant toggle fallback for `accessibilityReduceMotion`.
  - **Date & Time Rail**: Prominent day number, month, weekday, institutional Arabic day name, live ticking digital clock, and compact stats.
  - **Restrained Fatimid Architectural Motifs**: Vector-rendered 8-pointed star (`KhatamEightPointStar`) watermarks, architectural dividers, and Fatimid emerald/bronze jewel palette.
- **Schedule Adjustments Banner**:
  - Amber alert block highlighting all changes detected for the active day with one-click acknowledgement.
  - Integrated quick task creation field.
- **Weekly Timetable (`TimetableView`)**:
  - Segmented weekday selector (Monday through Saturday).
  - Period card detailing start/end time, subject, and instructor.
  - Change indicator badge for modified slots.
  - "Add Task for Class" modal sheet allowing 1-click task creation linked to that specific period.
- **Task Management (`TasksView`)**:
  - Filters: Pending, Completed, All.
  - Priority pills (Urgent, High, Medium, Low).
  - Subject tags linked to timetable courses.
  - **Interactive Task Discrepancy Banner**: Provides immediate visual warning and *Adopt*, *Keep*, and *Dismiss* action buttons.
- **Menu Bar Extra Companion (`MenuBarView`)**:
  - Fast-access status bar popover with `graduationcap` icon.
  - Next/Current class preview with change alert notes.
  - Quick task capture input from anywhere in macOS.
  - Top 4 pending tasks with completion checkboxes.
  - "Open SWIFT" and "Quit" shortcuts.

### 5. Persistence Layer (`SwiftPersistence`)
- **Actor-Isolated Thread-Safe File Repositories**:
  - `LocalFileTimetableRepository`: Persists `stored_timetable.json` atomically.
  - `LocalFileTaskRepository`: Persists `stored_tasks.json` atomically.
  - `LocalFileChangeRepository`: Persists change audit logs (`stored_changes.json`) with acknowledgment tracking.
  - Directory: `~/Library/Application Support/SWIFT/` (macOS standard).

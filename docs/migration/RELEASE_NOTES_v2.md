# Jadwal 2.0 (.NET 10 LTS & Avalonia UI) Release Notes
**Application**: Jadwal (جدول) — Academic Schedule & Timetable Companion  
**Version**: 2.0.0 (Release)  
**Supported Platforms**: Windows 10/11 (x64, ARM64) & macOS 12+ (x64, Apple Silicon)

---

## Highlights of Version 2.0

1. **Cross-Platform Native C# Engine**:
   - Built on **.NET 10 LTS** and **Avalonia UI 12.1+**.
   - First-class support for **Windows** and **macOS** with zero emulator or Python runtime dependencies.
   - Clean architecture separating Domain, Application, Infrastructure, UI, and Platform-specific adapters.

2. **Freeform Timetable Redesign**:
   - **Hero Date & Time Banner**: Displays Arabic weekday ("يوم الإثنين") in authentic **Kanz-al-Lulu** calligraphy, Gregorian date, day subtitle, live digital clock, and real-time class status pill.
   - **Two Horizontal Period Rows**:
     - Row 1: Morning prep and early sessions (Periods 1–5).
     - Row 2: Midday and afternoon sessions (Periods 6–10).
   - **Narrow Vertical Break Pills**: Clean vertical divider cards for Morning Prep, Recess, Lunch & Namaz, and Afternoon breaks with icon, stacked title, duration pill, and time ranges.
   - **Flip Card Interaction**: Clicking on the task badge of any class card flips the card to reveal linked homework and academic tasks with checkboxes.
   - **Academic Schedule Rules**:
     - **Monday–Thursday**: Full 10-period schedule with morning Physical Training.
     - **Friday (Jumua)**: 9 periods (starting with Period 2, no PT), Jumua break, and special Friday greeting.
     - **Saturday**: Half-day schedule concluding at 1:15 PM (Periods 1–4 in Row 1, Periods 5–8 in Row 2).

3. **Data Integrity & Task Protection**:
   - Preserves tasks linked to classes when subjects change or get cancelled by raising actionable discrepancies.
   - Atomic JSON repositories with backup snapshots.
   - Automated legacy data migration with rollback safety.

4. **Native Platform Integration**:
   - **Windows**: Windows DPAPI encrypted credential storage, WinRT / PowerShell toast notifications, and Windows startup run key integration.
   - **macOS**: macOS Keychain credential storage, AppleScript system notifications, and LaunchAgents plist startup integration.
   - **System Tray Companion**: Built-in system tray icon showing current class status and quick actions.

---

## How to Build & Run

### 1. Run in Development Mode
```bash
# Set .NET path if needed
export PATH="$HOME/.dotnet:$PATH"

# Run the desktop app
dotnet run --project src/Jadwal.App/Jadwal.App.csproj
```

### 2. Run Test Suite
```bash
dotnet test Jadwal.sln -c Release
```

### 3. Publish Self-Contained Executables

#### For Windows:
```bash
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r win-x64 --self-contained -o dist/win-x64
```
Output: `dist/win-x64/Jadwal.App.exe`

#### For macOS:
```bash
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r osx-x64 --self-contained -o dist/osx-x64
```
Output: `dist/osx-x64/Jadwal.App`

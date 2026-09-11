<p align="center">
  <img src="assets/icon.png" width="120" height="120" alt="Jadwal App Icon" />
</p>

<h1 align="center">Jadwal (جدول) — Academic Operating System</h1>

<p align="center">
  <b>Modern, cross-platform desktop application and automation engine for Aljamea-tus-Saifiyah students and faculty, featuring weekly timetable synchronization, Fatimid Misri Hijri calendar, Miqaats tracking, task management, and schedule change protection.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Version-4.1.0-0B4F39.svg" alt="Version 4.1.0" />
  <img src="https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-C5A059.svg" alt="Platform macOS and Windows" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg" alt=".NET 10" />
  <img src="https://img.shields.io/badge/UI-Avalonia%2012-purple.svg" alt="Avalonia UI" />
  <img src="https://img.shields.io/badge/Tests-87%20Passed-success.svg" alt="87 Tests Passed" />
</p>

---

## 🌟 Key Features

### 📅 Dual Fatimid Hijri & Gregorian Calendar
- **Astronomical Tabular Lunar Algorithm**: Implements the official Fatimid Misri 30-year tabular lunar calendar (11 leap years per cycle) with bidirectional conversions to/from Gregorian dates via Astronomical Julian Day numbers.
- **7-Column Interactive Monthly View**: Clean monthly calendar grid displaying both Hijri and Gregorian dates side by side with weekday headers (Ithnayn to Ahad).
- **Official 12-Month Miqaats Dataset**: Complete embedded dataset ([Data/miqaats.json](Data/miqaats.json)) covering all 12 Hijri months with event names, phases (Zohr, Maghrib, Tahajjud, Raat), categories, and detailed historical descriptions.
- **Daily Miqaats Integration**: Today view and Calendar view automatically display all Miqaats occurring on the selected date with dedicated badges and descriptions.

### ⏰ Real-Time Schedule & 12-Hour AM/PM Sync
- **Continuous Computer Clock Synchronization**: Active class timetable periods continuously synchronize with your computer's local clock in real time.
- **12-Hour AM/PM Formatting**: All class times and break intervals are displayed in standard 12-hour format with clear AM/PM indicators (e.g., `8:00 AM – 8:50 AM`, `1:00 PM – 1:50 PM`).
- **Dynamic Period Lifecycle**: Automatically calculates and updates class statuses:
  - 🟢 **In Session**: Currently active period with elapsed time indicators.
  - 🟡 **Starting Soon**: Upcoming within 15 minutes.
  - ⚪ **Upcoming**: Future periods for the day.
  - 🔘 **Completed**: Finished periods with subdued styling.
  - 🔴 **Changed**: Periods affected by schedule updates.

### 📐 Right-to-Left (RTL) Weekly Timetable
- **RTL Sequence**: Timetable grid flows naturally in Right-to-Left sequence matching Arabic reading order and traditional Jamea timetable formatting.
- **3-Row Daily Layout**:
  - **Row 1**: Morning periods (Periods 1, 2, 3) followed by Morning Break.
  - **Row 2**: Pre-noon periods (Periods 4, 5) followed by Zohr/Lunch Break.
  - **Row 3**: Afternoon periods (Periods 6, 7, 8) plus Physical Education.
- **Physical Education Slot**: Compact, dedicated sports card positioned after Period 8 with an exception rule for Friday (omitted on Fridays).
- **Horizontal & Vertical Break Banners**: Distinct break representations featuring contextual prayer and meal icons with full start and end times.

### 🔄 3D Class Card Flip & Subject Task Integration
- **Interactive Card Flip**: Click the flip button on any class card to rotate 180° on its Y-axis, revealing all tasks linked directly to that specific period.
- **Inline Task Creation**: Add homework, revision, or preparation tasks directly from the flipped card.
- **Tasks Filter by Subject**: The dedicated Tasks view allows instant filtering of all tasks by academic subject.

### 📝 Comprehensive Task Management & Edit Modal
- **Full Task Lifecycle**: Create, prioritize (Urgent, High, Medium, Low), schedule deadlines, complete, and edit tasks.
- **Edit Task Modal Dialog**: Full editing interface allowing modification of task titles, notes, priorities, due dates, and subject associations.
- **Timetable Change Protection (Zero Silent Data Loss)**: When schedule changes occur, tasks anchored to slot IDs are preserved, prompting the student to adopt the new subject, keep context, or dismiss.

### 🎨 Light & Dark Fatimid Islamic Aesthetic
- **Curated Color Palette**: Fatimid Islamic heritage design featuring deep emerald green (`#0B4F39`), warm cream surfaces (`#FAF8F5`), and brushed gold accents (`#C5A059`).
- **Authentic Arabic Typography**: Native integration of the **Kanzallulu** typeface for all Arabic text, day names, and decorative headers.
- **Theme Mode Selection**: Seamlessly toggle between Light and Dark modes in Settings, with preference persisted across app launches.
- **Smooth Micro-Animations**: Refined transitions for card flips, tab navigation, and modal dialogues.

### 🔄 Automated Jamia Portal Sync
- **Intelligent Authentication**: Single sign-on bridge powered by Playwright with secure local token persistence (`jamea_token.json`).
- **Smart Session Expiry Handling**: Sync only requests login when the security token has expired; otherwise performs instant headless background fetch.
- **Self-Contained Fallback**: Automatically preserves the cached timetable snapshot so schedules are always available offline.

### ⚙️ Settings & Credits
- **Developer Credits**: Dedicated credit section highlighting application metadata and developer attribution:
  - **App**: Jadwal (جدول)
  - **Developer**: Mustafa Rajkotwala
  - **Version**: 4.1.0
- **Launch at Startup**: Optional automatic system startup toggle on both macOS (LaunchAgent) and Windows (Registry Run).

---

## 🏗️ Architecture

Jadwal is built with **.NET 10** and **Avalonia UI 12**, following Clean Architecture principles:

```
JameaHelper/
├── src/
│   ├── Jadwal.Domain/           # Entities, Value Objects (FatimidHijriDate, ScheduleTimeline, Task, Timetable)
│   ├── Jadwal.Application/      # DTOs, Contracts (IJamiaAuthenticationService, IJamiaCredentialStore, etc.)
│   ├── Jadwal.Infrastructure/   # JSON Repositories, Embedded Miqaats Data, Data Migration
│   ├── Jadwal.Integrations.Jamia/# Native Microsoft.Playwright auth, Token storage, Direct HTTPS client, Excel parser
│   ├── Jadwal.Platform.MacOS/   # macOS Keychain secure storage, notifications, LaunchAgent startup
│   ├── Jadwal.Platform.Windows/ # Windows DPAPI storage, toast notifications, Registry startup
│   ├── Jadwal.UI/               # Avalonia MVVM Views, ViewModels, Fatimid theme tokens, Kanzallulu font
│   └── Jadwal.App/              # Application entry point, DI configuration, native launcher
├── tests/                       # 5 comprehensive test projects (87 unit & integration tests)
├── dist/                        # Packaged macOS (.app, .zip) and Windows (win-x64) release bundles
├── Data/                        # Official miqaats.json dataset
├── assets/                      # Official application icons (PNG & ICNS)
├── tools/legacy/                # Preserved legacy migration references
├── backups/                     # Preserved legacy archives
└── Jadwal.sln                   # Visual Studio / dotnet solution
```

---

## 🚀 Quick Start for Users

> 💡 **No Python required!** Jadwal runs completely on native .NET with embedded Playwright browser automation.

### macOS
1. Download **`Jadwal-v4.1.0-macos-x64.zip`** from [Releases](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases).
2. Unzip the file and move **`Jadwal.app`** to `/Applications`.
3. Double-click to open. If prompted by macOS Gatekeeper, right-click and choose **Open**.
4. Click **Settings > Sync Jamia Timetable** to download your official schedule.

### Windows
1. Download **`Jadwal-v4.1.0-windows-x64.zip`** from [Releases](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases).
2. Extract the archive.
3. Launch **`Jadwal.App.exe`**.

---

## 🛠️ Developer Setup & Build

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 1. Build and Run Tests
```bash
dotnet test Jadwal.sln -c Release
```

### 3. Run Locally
```bash
dotnet run --project src/Jadwal.App/Jadwal.App.csproj
```

### 4. Publish Release Bundles
```bash
# Publish for macOS (self-contained x64)
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r osx-x64 --self-contained true -o dist/osx-x64

# Publish for Windows (self-contained x64)
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
```

---

## 🔒 Security & Privacy
- **Local Storage**: All timetable data, student tasks, and authentication tokens are stored strictly on your local computer (`~/Library/Application Support/Jadwal/` on macOS and `%APPDATA%\Jadwal\` on Windows).
- **Secure Credentials**: Sensitive session tokens are protected using platform OS primitives (macOS Keychain / Apple Data Protection and Windows DPAPI).
- **Zero Third-Party Telemetry**: Jadwal does not transmit any student data or timetable information to any external server other than the official Jamea portal during sync.

---

## 👤 Credits & Attribution
- **Developer**: Mustafa Rajkotwala
- **Institution**: Aljamea-tus-Saifiyah
- **Version**: 4.0.0

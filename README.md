<p align="center">
  <img src="assets/icon.png" width="120" height="120" alt="Jadwal App Icon" />
</p>

<h1 align="center">Jadwal — Academic Operating System</h1>

<p align="center">
  <b>Native macOS application and automation pipeline for Aljamea-tus-Saifiyah weekly timetables, task protection, and Menu Bar tracking.</b>
</p>

---

## 🌟 Key Features

- **Automated Jamea Portal Sync**:
  - Secure Playwright-based single sign-on with session token persistence.
  - Automatic download and parsing of the official weekly timetable spreadsheet.
  - Automatic `401 AUTH_EXPIRED` detection with zero-friction re-authentication.
- **Timetable Change Management & Task Protection**:
  - Stable slot-based period identity that survives schedule changes.
  - Granular diff engine detecting `SUBJECT_CHANGED`, `TIME_CHANGED`, `DETAILS_CHANGED`, `ADDED`, and `REMOVED`.
  - **Zero Silent Data Loss**: Tasks linked to periods preserve student notes and prompt the user with 1-click resolution (*Adopt New Subject*, *Keep Original Context*, or *Dismiss*).
- **Today Class Timetable Layout**:
  - **No-Scroll Guarantee**: Two-column responsive card grid fitting the entire day on standard macOS window sizes.
  - **Dominant Subject Typography**: Clear, large, bold typography readable from a distance.
  - **Live Class Status**: Automatically calculates `In Session`, `Starting Soon`, `Completed`, `Upcoming`, or `Changed`.
  - **3D Card Flip**: Vertical-axis flip showing tasks attached to that specific class, with inline task addition.
  - **Date & Time Rail**: Live ticking clock, day number, Arabic day name, and day summary metrics.
  - **Restrained Fatimid Architectural Motifs**: Vector 8-pointed star watermarks and arch accents.
- **macOS Menu Bar Extra Companion**:
  - Real-time Next / Current Class preview right in the macOS menu bar.
  - Quick task capture with instant `Return` key submission.
  - Top priority action items with direct completion toggles.
- **Native SwiftUI Architecture**:
  - Clean NavigationSplitView sidebar navigation.
  - High performance, low memory footprint, and dark/light mode support.
  - Thread-safe actor-isolated JSON persistence under `~/Library/Application Support/Jadwal/`.

---

## 🚀 Quick Start for Users

### Prerequisites
- macOS 14.0+ (Sonoma or Sequoia)
- Python 3.10+

### 1. Install Dependencies
Run the automated setup script to install Playwright and parsing libraries:
```bash
git clone https://github.com/mustafarajkotwala12-cmyk/Jadwal.git
cd Jadwal
./setup.sh
```
*Or manually:*
```bash
python3 -m pip install -r requirements.txt
python3 -m playwright install chromium
```

### 2. Run the App
- **Download the latest release:** Download `Jadwal-v1.0.0-macOS.zip` from [Releases](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases).
- Unzip and double-click **`Jadwal.app`** (or move it to `/Applications`).
- Click **"Sync"** to log in through ITS and import your live weekly schedule.

---

## 🛠️ Building From Source

### Build & Bundle Standalone `.app`
```bash
cd Jadwal
./scripts/bundle_app.sh
```
The compiled, code-signed bundle and release zip will be generated at:
- `Jadwal/build/Jadwal.app`
- `Jadwal/build/Jadwal-v1.0.0-macOS.zip`

### Open in Xcode
```bash
open Jadwal/Package.swift
```
Select the **JadwalApp** scheme and press `Cmd + R` to run.

---

## 📂 Project Structure

```
Jadwal/
├── helper/
│   └── jamea_helper.py         # Playwright ITS auth & Excel schedule parser
├── Jadwal/
│   ├── Package.swift           # Swift 6 modular package definition
│   ├── Packages/
│   │   ├── JadwalCore/         # Domain models (Timetable, Task, Timeline, DiffEngine)
│   │   ├── JadwalPersistence/  # Actor-isolated atomic file storage
│   │   └── JadwalIntegrations/ # Jamea Helper Process bridge & JSON importer
│   ├── macOSApp/               # SwiftUI views (Today, Timetable, Tasks, MenuBar)
│   └── scripts/
│       └── bundle_app.sh       # Release packaging script (.app & .zip)
├── data/
│   └── timetable.json          # Normalized weekly schedule schema
├── setup.sh                    # 1-click Python & Playwright installer
└── requirements.txt            # Python dependencies
```

---

## 🔒 Privacy & Credentials
- Authentication tokens (`jamea_token.json`) and local browser session profiles are stored strictly on your local machine and are ignored by Git.
- No personal data or credentials are ever transmitted outside the official Jamea portal.

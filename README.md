# SWIFT — Academic Operating System for Aljamea-tus-Saifiyah

**SWIFT** is a native macOS application and automation pipeline designed to track weekly timetables, manage academic tasks, detect schedule changes non-destructively, and provide a lightweight Menu Bar companion for students.

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
- **macOS Menu Bar Extra Companion**:
  - Real-time Next / Current Class preview right in the macOS menu bar.
  - Quick task capture with instant `Return` key submission.
  - Top priority action items with direct completion toggles.
- **Native SwiftUI Architecture**:
  - Clean NavigationSplitView sidebar navigation.
  - High performance, low memory footprint, and dark/light mode support.
  - Thread-safe actor-isolated JSON persistence under `~/Library/Application Support/SWIFT/`.

---

## 🚀 Quick Start for Users

### Prerequisites
- macOS 14.0+ (Sonoma or Sequoia)
- Python 3.10+

### 1. Install Dependencies
Run the automated setup script to install Playwright and parsing libraries:
```bash
git clone https://github.com/<YOUR_USERNAME>/JameaHelper.git
cd JameaHelper
./setup.sh
```
*Or manually:*
```bash
python3 -m pip install -r requirements.txt
python3 -m playwright install chromium
```

### 2. Run the App
- **Download the latest release:** Download `SWIFT-v1.0.0-macOS.zip` from [Releases](https://github.com/<YOUR_USERNAME>/JameaHelper/releases).
- Unzip and double-click **`SWIFT.app`** (or move it to `/Applications`).
- Click **"Sync"** to log in through ITS and import your live weekly schedule.

---

## 🛠️ Building From Source

### Build & Bundle Standalone `.app`
```bash
cd SWIFT
./scripts/bundle_app.sh
```
The compiled, code-signed bundle and release zip will be generated at:
- `SWIFT/build/SWIFT.app`
- `SWIFT/build/SWIFT-v1.0.0-macOS.zip`

### Open in Xcode
```bash
open SWIFT/Package.swift
```
Select the **SWIFTApp** scheme and press `Cmd + R` to run.

---

## 📂 Project Structure

```
JameaHelper/
├── helper/
│   └── jamea_helper.py         # Playwright ITS auth & Excel schedule parser
├── SWIFT/
│   ├── Package.swift           # Swift 6 modular package definition
│   ├── Packages/
│   │   ├── SwiftCore/          # Domain models (Timetable, Task, DiffEngine)
│   │   ├── SwiftPersistence/   # Actor-isolated atomic file storage
│   │   └── SwiftIntegrations/  # Jamea Helper Process bridge & JSON importer
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

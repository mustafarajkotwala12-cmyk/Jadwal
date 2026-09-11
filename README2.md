# 📖 Jadwal (جدول) — Easy Download & Setup Guide

Welcome to **Jadwal (جدول)**! This step-by-step guide will walk you through downloading, installing, and setting up Jadwal on **Windows** and **macOS**.

> 🚀 **No Python Required!** Starting with version 4.0.0, Jadwal features a 100% native .NET sync engine. You do **not** need to install Python, pip, or run command-line setup scripts.

---

## ⚡ Quick Navigation
- [🪟 Windows Setup Guide](#-windows-installation-guide)
- [🍏 macOS Setup Guide](#-macos-installation-guide)
- [❓ Frequently Asked Questions & Troubleshooting](#-troubleshooting--faq)

---

## 🪟 Windows Installation Guide

Follow these 2 simple steps to get Jadwal running on Windows 10 or Windows 11:

### Step 1: Download the Jadwal Windows App
1. Go to the **[Jadwal GitHub Releases page](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases)**.
2. Under the latest release (v4.0.0), download:
   - 📦 **`Jadwal-v4.0.0-windows-x64.zip`**
3. Once downloaded, **Right-click** the ZIP file and select **"Extract All..."**, then click **Extract**.

---

### Step 2: Open Jadwal!
1. Open the extracted folder and double-click **`Jadwal.App.exe`** to launch the app.
2. Navigate to **⚙️ Settings** on the left menu:
   - Enter your **ITS ID** and **Password** under *Jamia Portal Authentication*.
   - Click **Save Credentials**.
   - Click **Sync Jamia Timetable**.
3. A browser window will appear to authenticate with the Jamia portal. If prompted, complete your ITS sign-in. Jadwal will automatically capture your schedule, save it securely, and display it on your dashboard!

---

## 🍏 macOS Installation Guide

Follow these 2 simple steps to install Jadwal on your Mac (macOS Monterey, Ventura, Sonoma, Sequoia):

### Step 1: Download Jadwal for Mac
1. Go to the **[Jadwal GitHub Releases page](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases)**.
2. Under the latest release (v4.0.0), download:
   - 📦 **`Jadwal-v4.0.0-macos-x64.zip`**
3. Double-click the downloaded `.zip` file to unzip it. You will see **`Jadwal.app`**.
4. Drag **`Jadwal.app`** into your **`Applications`** folder.

---

### Step 2: Launch Jadwal
1. Open your **Applications** folder and look for **Jadwal**.
2. ⚠️ **First Time Launch on macOS (Gatekeeper)**:
   - **Right-click** (or `Control + Click`) on **Jadwal.app**.
   - Select **Open** from the menu.
   - A dialog will ask *"Are you sure you want to open it?"* — click **Open**.
   *(You only need to do this the first time; afterwards you can click it normally like any other Mac app).*
3. In Jadwal, click **⚙️ Settings** on the left menu:
   - Enter your **ITS ID** and **Password**.
   - Click **Save Credentials**.
   - Click **Sync Jamia Timetable**.
4. Sign in through the browser window that opens. Jadwal will capture your schedule and set up your daily dashboard!

---

## ❓ Troubleshooting & FAQ

### 1. "Do I need Python installed?"
- **No!** Jadwal runs completely on native .NET with embedded browser automation. No Python runtime, pip packages, or virtual environments are needed.

### 2. "App cannot be opened because it is from an unidentified developer" (macOS)
- **Solution**: Do not double-click. Instead, **Right-click** (or `Control + Click`) the `Jadwal.app` icon, click **Open**, and then click **Open** in the confirmation popup.

### 3. "Browser closed before login completed"
- **Solution**: When clicking *Sync Jamia Timetable*, a browser window will launch. Please do not close this window manually. Allow the page to load, sign in with your ITS credentials, and let the window finish syncing by itself. Once sync succeeds, the window closes automatically.

### 4. How often does Jadwal refresh my timetable?
- Jadwal includes an **Auto-Sync** feature that silently checks the Jamia portal every **4 minutes** (configurable from 3 to 10 minutes in Settings). Any room changes, substitutions, or period modifications will update on your screen automatically without opening a browser window as long as your session remains valid!

### 5. Can I use Jadwal offline?
- **Yes!** Once your timetable is synced, it is saved securely on your local computer (`%APPDATA%\Jadwal` on Windows, `~/Library/Application Support/Jadwal` on macOS). You can view your classes, tasks, and the Fatimid Hijri calendar even without an internet connection.

---

## 👤 Support & Credits
- **Developed by**: Mustafa Rajkotwala
- **Institution**: Aljamea-tus-Saifiyah
- **Repository**: [github.com/mustafarajkotwala12-cmyk/Jadwal](https://github.com/mustafarajkotwala12-cmyk/Jadwal)

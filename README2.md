# 📖 Jadwal (جدول) — Easy Download & Setup Guide

Welcome to **Jadwal (جدول)**! This step-by-step guide will walk you through downloading, installing, and setting up Jadwal on **Windows** and **macOS** as easily as possible.

---

## ⚡ Quick Navigation
- [🪟 Windows Setup Guide](#-windows-installation-guide)
- [🍏 macOS Setup Guide](#-macos-installation-guide)
- [❓ Frequently Asked Questions & Troubleshooting](#-troubleshooting--faq)

---

## 🪟 Windows Installation Guide

Follow these 4 simple steps to get Jadwal running on Windows 10 or Windows 11:

### Step 1: Install Python 3 (One-Time Requirement)
Jadwal uses Python to securely communicate with the Jamia portal and sync your timetable.

1. Go to the official Python website: **[python.org/downloads](https://www.python.org/downloads/)**
2. Click the yellow button: **"Download Python 3.x.x"**.
3. Open the downloaded installer file.
4. ⚠️ **CRITICAL STEP**: At the bottom of the installer window, check the box that says:
   > ☑️ **"Add python.exe to PATH"** (or **"Add Python to environment variables"**)
5. Click **"Install Now"** and wait for it to complete.

---

### Step 2: Download the Jadwal Windows App
1. Go to the **[Jadwal GitHub Releases page](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases)**.
2. Under the latest release (v4.0.0), download:
   - 📦 **`Jadwal-v4.0.0-windows-x64.zip`**
3. Once downloaded, **Right-click** the ZIP file and select **"Extract All..."**, then click **Extract**.

---

### Step 3: Run the Setup File
Inside the extracted folder, you will see a file named `setup.bat`:

1. Double-click **`setup.bat`**.
2. A black terminal window will open and automatically install all needed tools (Playwright, Pandas, OpenPyXL, and browser components).
3. When it finishes and says *"Setup completed successfully!"*, press any key to close the window.

> 💡 *Note: You only ever need to run `setup.bat` once.*

---

### Step 4: Open Jadwal!
1. Double-click **`Jadwal.App.exe`** to open the app.
2. Navigate to **⚙️ Settings**:
   - Enter your **ITS ID** and **Password** under *Jamia Portal Authentication*.
   - Click **Save Credentials**.
   - Click **Sync Jamia Timetable**.
3. A browser window will appear. If prompted, complete your ITS sign-in. Jadwal will automatically capture your schedule and display it on your dashboard!

---

---

## 🍏 macOS Installation Guide

Follow these simple steps to install Jadwal on your Mac (macOS Monterey, Ventura, Sonoma, Sequoia):

### Step 1: Verify Python 3
Macs often have Python pre-installed, or you can install it in 30 seconds:

1. Open **Terminal** (press `Cmd + Space`, type `Terminal`, and press `Enter`).
2. Type the following and press `Enter`:
   ```bash
   python3 --version
   ```
3. If Python is installed, it will print something like `Python 3.12.x`.
4. If you don't have Python or are prompted to install Developer Tools, you can easily download the official Mac installer from **[python.org/downloads/macos](https://www.python.org/downloads/macos/)** (choose the *"macOS 64-bit universal2 installer"*).

---

### Step 2: Download Jadwal for Mac
1. Go to the **[Jadwal GitHub Releases page](https://github.com/mustafarajkotwala12-cmyk/Jadwal/releases)**.
2. Under the latest release (v4.0.0), download:
   - 📦 **`Jadwal-v4.0.0-macos-x64.zip`**
3. Double-click the downloaded `.zip` file to unzip it. You will see **`Jadwal.app`**.
4. Drag **`Jadwal.app`** into your **`Applications`** folder.

---

### Step 3: Run the Dependencies Setup (One-Time)
1. Open **Terminal**.
2. Run this command to install the portal sync engine:
   ```bash
   pip3 install --upgrade pip && pip3 install playwright pandas openpyxl && python3 -m playwright install chromium
   ```
3. Wait for the terminal to display that the installation succeeded.

---

### Step 4: Launch Jadwal
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
4. Log into ITS in the browser window that opens. Jadwal will download your schedule and set up your daily dashboard!

---

---

## ❓ Troubleshooting & FAQ

### 1. "Python was not recognized / not detected" (Windows)
- **Cause**: You may have installed Python without ticking the *"Add python.exe to PATH"* checkbox.
- **Solution**: Re-run the Python installer from [python.org](https://www.python.org/downloads/), choose **Modify**, make sure **"Add to PATH"** is checked, and click Next. Then run `setup.bat` again.

### 2. "App cannot be opened because it is from an unidentified developer" (macOS)
- **Solution**: Do not double-click. Instead, **Right-click** (or `Control + Click`) the `Jadwal.app` icon, click **Open**, and then click **Open** in the confirmation popup.

### 3. "Browser closed before login completed"
- **Solution**: When clicking *Sync Jamia Timetable*, a browser window will launch. Please do not close this window manually. Allow the page to load, sign in with your ITS credentials, and let the window finish syncing by itself.

### 4. How often does Jadwal refresh my timetable?
- Jadwal includes an **Auto-Sync** feature that silently checks the Jamia portal every **4 minutes** (configurable from 3 to 10 minutes in Settings). Any room changes, substitutions, or period modifications will update on your screen automatically.

### 5. Can I use Jadwal offline?
- **Yes!** Once your timetable is synced, it is saved securely on your local computer (`%APPDATA%\Jadwal` on Windows, `~/Library/Application Support/Jadwal` on macOS). You can view your classes, tasks, and the Fatimid Hijri calendar even without an internet connection.

---

## 👤 Support & Credits
- **Developed by**: Mustafa Rajkotwala
- **Institution**: Aljamea-tus-Saifiyah
- **Repository**: [github.com/mustafarajkotwala12-cmyk/Jadwal](https://github.com/mustafarajkotwala12-cmyk/Jadwal)

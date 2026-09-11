@echo off
echo ===================================================
echo   Jadwal / Jamea Helper Windows Environment Setup
echo ===================================================
echo.

:: Check for py launcher or python
where py >nul 2>nul
if %errorlevel% equ 0 (
    set PYTHON_CMD=py
    goto :found_python
)

where python >nul 2>nul
if %errorlevel% equ 0 (
    set PYTHON_CMD=python
    goto :found_python
)

echo [ERROR] Python was not detected on this system.
echo Please install Python 3 (version 3.10 or newer) from https://www.python.org/
echo Make sure to check "Add Python to PATH" during installation.
echo.
pause
exit /b 1

:found_python
echo [INFO] Using Python command: %PYTHON_CMD%
%PYTHON_CMD% --version
echo.

echo [INFO] Upgrading pip...
%PYTHON_CMD% -m pip install --upgrade pip

echo [INFO] Installing required dependencies (Playwright, Pandas, OpenPyXL)...
%PYTHON_CMD% -m pip install -r requirements.txt
if %errorlevel% neq 0 (
    echo [ERROR] Failed to install Python dependencies.
    pause
    exit /b %errorlevel%
)

echo.
echo [INFO] Installing Playwright Chromium browser binary...
%PYTHON_CMD% -m playwright install chromium
if %errorlevel% neq 0 (
    echo [WARNING] Playwright Chromium installation encountered a warning.
    echo If Google Chrome or Microsoft Edge is already installed, Jamea Helper will use it automatically.
)

echo.
echo ===================================================
echo   Setup completed successfully!
echo   You can now run Jadwal.App.exe and sync schedule.
echo ===================================================
echo.
pause

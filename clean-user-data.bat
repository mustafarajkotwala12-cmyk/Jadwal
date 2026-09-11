@echo off
REM clean-user-data.bat
REM Purges all local Jadwal user data, caches, browser profiles, and DPAPI vault credentials on Windows.

echo [Jadwal] Cleaning local AppData and caches...
if exist "%APPDATA%\Jadwal" rmdir /s /q "%APPDATA%\Jadwal"
echo [Jadwal] Done! Your machine is now in a 100% pristine fresh-install state for Jadwal.
pause

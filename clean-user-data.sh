#!/bin/bash
# clean-user-data.sh
# Purges all local Jadwal user data, caches, browser profiles, and OS keychain credentials
# on macOS, returning the machine to a 100% clean factory installation state.

set -e

echo "🧹 [Jadwal] Cleaning OS vault credentials from macOS Keychain..."
/usr/bin/security delete-generic-password -s "com.jadwal.app" -a "its_id" 2>/dev/null || true
/usr/bin/security delete-generic-password -s "com.jadwal.app" -a "its_password" 2>/dev/null || true
/usr/bin/security delete-generic-password -s "com.jadwal.app" -a "jamea_access_token" 2>/dev/null || true

echo "🧹 [Jadwal] Removing local Application Support data & caches..."
rm -rf "$HOME/Library/Application Support/Jadwal"

echo "✅ Done! Your machine is now in a 100% pristine fresh-install state for Jadwal."

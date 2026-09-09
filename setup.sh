#!/usr/bin/env bash
set -e

echo "🚀 Setting up Jamea Helper & SWIFT dependencies..."

# Verify Python 3
if ! command -v python3 &> /dev/null; then
    echo "❌ Error: python3 is not installed. Please install Python 3 (e.g. from python.org or 'brew install python')."
    exit 1
fi

echo "📦 Installing Python requirements (Playwright, Pandas, OpenPyXL)..."
python3 -m pip install --upgrade pip
python3 -m pip install -r requirements.txt

echo "🌐 Installing Playwright Chromium browser binary..."
python3 -m playwright install chromium

echo "✅ Environment setup complete!"
echo "You can now run SWIFT.app or sync your timetable."

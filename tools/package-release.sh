#!/bin/bash
# package-release.sh
# Builds, bundles, and packages clean distribution release zip archives for Jadwal v4.1.0

set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

export PATH="$PATH:/usr/local/share/dotnet:$HOME/.dotnet"
PLAYWRIGHT_PKG="$HOME/.nuget/packages/microsoft.playwright/1.62.0/.playwright"

echo "=== 1. Running Tests ==="
dotnet test Jadwal.sln -c Release

echo "=== 2. Publishing macOS Release (osx-x64) ==="
rm -rf dist/osx-x64 dist/Jadwal.app
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r osx-x64 --self-contained false -o dist/osx-x64

# Copy Playwright driver package
if [ ! -d "dist/osx-x64/.playwright" ]; then
    mkdir -p dist/osx-x64/.playwright
    cp -R src/Jadwal.App/bin/Release/net10.0/osx-x64/.playwright/* dist/osx-x64/.playwright/
fi

echo "=== 3. Creating Jadwal.app Bundle ==="
mkdir -p dist/Jadwal.app/Contents/MacOS
mkdir -p dist/Jadwal.app/Contents/Resources

cp assets/AppIcon.icns dist/Jadwal.app/Contents/Resources/AppIcon.icns

cat << 'EOF' > dist/Jadwal.app/Contents/Info.plist
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Jadwal</string>
    <key>CFBundleDisplayName</key>
    <string>Jadwal (جدول)</string>
    <key>CFBundleIdentifier</key>
    <string>com.jadwal.app</string>
    <key>CFBundleVersion</key>
    <string>4.1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>4.1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>Jadwal.App</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

cp -R dist/osx-x64/* dist/Jadwal.app/Contents/MacOS/
if [ -d "dist/osx-x64/.playwright" ]; then
    cp -R dist/osx-x64/.playwright dist/Jadwal.app/Contents/MacOS/
fi

# Ensure both darwin-x64 and darwin-arm64 node binaries are present for universal macOS support
if [ -d "$PLAYWRIGHT_PKG/node/darwin-arm64" ]; then
    mkdir -p dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-arm64
    cp -R "$PLAYWRIGHT_PKG/node/darwin-arm64/"* dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-arm64/
    chmod +x dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-arm64/node 2>/dev/null || true
fi
if [ -d "$PLAYWRIGHT_PKG/node/darwin-x64" ]; then
    mkdir -p dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-x64
    cp -R "$PLAYWRIGHT_PKG/node/darwin-x64/"* dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-x64/
    chmod +x dist/Jadwal.app/Contents/MacOS/.playwright/node/darwin-x64/node 2>/dev/null || true
fi

# Move native binary to Jadwal.App.bin and create wrapper script
mv dist/Jadwal.app/Contents/MacOS/Jadwal.App dist/Jadwal.app/Contents/MacOS/Jadwal.App.bin
chmod +x dist/Jadwal.app/Contents/MacOS/Jadwal.App.bin

cat << 'EOF' > dist/Jadwal.app/Contents/MacOS/Jadwal.App
#!/bin/bash
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PLAYWRIGHT_DRIVER_SEARCH_PATH="$DIR"
export DOTNET_ROOT="/usr/local/share/dotnet"
if [ ! -d "$DOTNET_ROOT" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
fi
export PATH="$DOTNET_ROOT:$PATH"
exec "$DIR/Jadwal.App.bin" "$@"
EOF
chmod +x dist/Jadwal.app/Contents/MacOS/Jadwal.App

echo "=== 4. Publishing Windows Release (win-x64) ==="
rm -rf dist/win-x64
dotnet publish src/Jadwal.App/Jadwal.App.csproj -c Release -r win-x64 --self-contained false -o dist/win-x64

if [ ! -d "dist/win-x64/.playwright" ]; then
    mkdir -p dist/win-x64/.playwright
    cp -R src/Jadwal.App/bin/Release/net10.0/win-x64/.playwright/* dist/win-x64/.playwright/
fi

# Ensure Windows node.exe is present in win-x64
if [ ! -f "dist/win-x64/.playwright/node/win32_x64/node.exe" ] && [ -f "$PLAYWRIGHT_PKG/node/win32_x64/node.exe" ]; then
    mkdir -p dist/win-x64/.playwright/node/win32_x64
    cp "$PLAYWRIGHT_PKG/node/win32_x64/node.exe" dist/win-x64/.playwright/node/win32_x64/
fi

echo "=== 5. Creating Clean Release Zip Archives ==="
rm -f dist/Jadwal-v4.0.0-*.zip dist/Jadwal-v4.1.0-*.zip

cd dist
zip -r -q Jadwal-v4.1.0-macos-x64.zip Jadwal.app
zip -r -q Jadwal-v4.1.0-windows-x64.zip win-x64
cd "$REPO_ROOT"

echo "=== 6. Auditing Release Archives for Credentials/Data ==="
UNEXPECTED_MAC=$(unzip -l dist/Jadwal-v4.1.0-macos-x64.zip | grep -i -E "token|cred|password|secure_store|timetable\.json|tasks\.json" || true)
if [ -n "$UNEXPECTED_MAC" ]; then
    echo "❌ ERROR: Found user credentials or state in macOS release zip:"
    echo "$UNEXPECTED_MAC"
    exit 1
fi

UNEXPECTED_WIN=$(unzip -l dist/Jadwal-v4.1.0-windows-x64.zip | grep -i -E "token|cred|password|secure_store|timetable\.json|tasks\.json" || true)
if [ -n "$UNEXPECTED_WIN" ]; then
    echo "❌ ERROR: Found user credentials or state in Windows release zip:"
    echo "$UNEXPECTED_WIN"
    exit 1
fi

echo "✅ SUCCESS: All Jadwal v4.1.0 release archives built and verified 100% clean!"
ls -lh dist/*.zip

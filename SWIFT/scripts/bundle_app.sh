#!/usr/bin/env bash
set -e

# Change to SWIFT directory
cd "$(dirname "$0")/.."

APP_NAME="SWIFT"
BUNDLE_DIR="build/${APP_NAME}.app"
CONTENTS_DIR="${BUNDLE_DIR}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"

echo "🔨 Compiling release binary with SwiftPM..."
swift build -c release

echo "📦 Creating macOS App Bundle structure..."
rm -rf "${BUNDLE_DIR}"
mkdir -p "${MACOS_DIR}"
mkdir -p "${RESOURCES_DIR}"

# Copy compiled executable
cp ".build/release/SWIFTApp" "${MACOS_DIR}/${APP_NAME}"
chmod +x "${MACOS_DIR}/${APP_NAME}"

# Embed Python helper pipeline into App Bundle Resources
echo "🐍 Embedding Jamea Helper pipeline..."
mkdir -p "${RESOURCES_DIR}/helper"
cp "../helper/jamea_helper.py" "${RESOURCES_DIR}/helper/"

# Embed App Icon
echo "🎨 Embedding App Icon..."
if [ -f "macOSApp/Resources/AppIcon.icns" ]; then
    cp "macOSApp/Resources/AppIcon.icns" "${RESOURCES_DIR}/AppIcon.icns"
fi

# Create Info.plist
cat <<EOF > "${CONTENTS_DIR}/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIconName</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>com.jamea.swift</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.education</string>
</dict>
</plist>
EOF

# Ad-hoc code sign for local execution
echo "🔏 Applying ad-hoc code signature..."
codesign --force --deep -s - "${BUNDLE_DIR}"

# Create GitHub Release Zip Package
ZIP_NAME="SWIFT-v1.0.0-macOS.zip"
echo "📦 Packaging ${ZIP_NAME} for GitHub Release..."
(cd build && rm -f "${ZIP_NAME}" && zip -r -q -y "${ZIP_NAME}" "${APP_NAME}.app")

echo "✅ Successfully built: ${PWD}/${BUNDLE_DIR}"
echo "🎉 Release Archive: ${PWD}/build/${ZIP_NAME}"
echo "Ready for distribution and GitHub Release upload!"

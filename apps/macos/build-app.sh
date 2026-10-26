#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
bundle="$root/.local/LookPause.app"
swift build --package-path "$root/apps/macos"
mkdir -p "$bundle/Contents/MacOS"
cp "$root/apps/macos/.build/debug/LookPauseMediaProbe" "$bundle/Contents/MacOS/.LookPause.new"
mv -f "$bundle/Contents/MacOS/.LookPause.new" "$bundle/Contents/MacOS/LookPause"
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.lookpause.pilot</string>
<key>CFBundleName</key><string>PausePal</string>
<key>CFBundleDisplayName</key><string>PausePal</string>
<key>CFBundleExecutable</key><string>LookPause</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.1.0</string>
<key>CFBundleVersion</key><string>7</string>
<key>LSMinimumSystemVersion</key><string>15.0</string>
<key>LSUIElement</key><true/>
<key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
<key>NSLocalNetworkUsageDescription</key><string>接收你在家庭局域网中已批准手机的观看方向，不接收图像。</string>
<key>NSBonjourServices</key><array><string>_lookpause._tcp</string></array>
</dict></plist>
PLIST
bash "$root/apps/macos/sign-bundle.sh" "$bundle" com.lookpause.pilot
"$bundle/Contents/MacOS/LookPause" self-test

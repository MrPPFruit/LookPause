#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
bundle="$root/.local/LookPauseMediaProbe.app"
swift build --package-path "$root/apps/macos"
mkdir -p "$bundle/Contents/MacOS"
cp "$root/apps/macos/.build/debug/LookPauseMediaProbe" "$bundle/Contents/MacOS/LookPauseMediaProbe"
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.lookpause.media-probe</string>
<key>CFBundleName</key><string>LookPauseMediaProbe</string>
<key>CFBundleExecutable</key><string>LookPauseMediaProbe</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSUIElement</key><true/>
<key>NSAppleEventsUsageDescription</key><string>仅用于验证当前前台 QuickTime 测试视频的播放状态及明确暂停、播放。</string>
</dict></plist>
PLIST
codesign --force --sign - --identifier com.lookpause.media-probe "$bundle"
"$bundle/Contents/MacOS/LookPauseMediaProbe" self-test

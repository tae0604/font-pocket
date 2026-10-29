#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
VERSION=0.2.2
OUT="$PWD/dist"
APP="$OUT/Font Pocket.app"
swift build -c release --arch arm64 --arch x86_64
BIN="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN/FontPocket" "$APP/Contents/MacOS/FontPocket"
find "$BIN" -maxdepth 1 -name '*.bundle' -exec cp -R '{}' "$APP/Contents/Resources/" \;
cp Sources/FontPocketMac/Resources/FontPocket-logo.png "$APP/Contents/Resources/FontPocket-logo.png"
cp LICENSE "$APP/Contents/Resources/LICENSE.txt"
ICONSET="$(mktemp -d)/FontPocket.iconset"
mkdir -p "$ICONSET"
for SIZE in 16 32 128 256 512; do
  sips -Z "$SIZE" Sources/FontPocketMac/Resources/FontPocket-logo.png --out "$ICONSET/icon_${SIZE}x${SIZE}.png" >/dev/null
  sips --padToHeightWidth "$SIZE" "$SIZE" "$ICONSET/icon_${SIZE}x${SIZE}.png" >/dev/null
  DOUBLE=$((SIZE*2))
  sips -Z "$DOUBLE" Sources/FontPocketMac/Resources/FontPocket-logo.png --out "$ICONSET/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
  sips --padToHeightWidth "$DOUBLE" "$DOUBLE" "$ICONSET/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/FontPocket.icns"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>FontPocket</string>
<key>CFBundleIdentifier</key><string>io.github.tae0604.font-pocket</string>
<key>CFBundleName</key><string>Font Pocket</string>
<key>CFBundleDisplayName</key><string>Font Pocket</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>CFBundleVersion</key><string>4</string>
<key>CFBundleIconFile</key><string>FontPocket.icns</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
<key>CFBundleDocumentTypes</key><array><dict>
<key>CFBundleTypeName</key><string>Font files and ZIP archives</string>
<key>CFBundleTypeRole</key><string>Viewer</string>
<key>LSHandlerRank</key><string>Alternate</string>
<key>CFBundleTypeExtensions</key><array><string>ttf</string><string>otf</string><string>ttc</string><string>otc</string><string>zip</string><string>woff</string><string>woff2</string><string>sfnt</string></array>
</dict></array>
</dict></plist>
PLIST
# Ad-hoc signing ensures the universal app has valid local code signatures. This is
# NOT Developer ID signing / notarization and is explicitly labeled as a preview.
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
lipo "$APP/Contents/MacOS/FontPocket" -verify_arch arm64 x86_64
ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUT/FontPocket-macOS-$VERSION-universal.zip"
shasum -a 256 "$OUT/FontPocket-macOS-$VERSION-universal.zip" > "$OUT/FontPocket-macOS-$VERSION-universal.zip.sha256"
printf 'Built %s\n' "$APP"

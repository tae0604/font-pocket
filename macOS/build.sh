#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
VERSION=0.3.0
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
<key>CFBundleVersion</key><string>5</string>
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
# A release machine can use an existing Developer ID identity and notarytool
# keychain profile. No private key or Apple ID is copied into the app/artifacts.
if [[ -n "${DEVELOPER_ID_IDENTITY:-}" ]]; then
  codesign --force --options runtime --timestamp --sign "$DEVELOPER_ID_IDENTITY" "$APP"
else
  if [[ -n "${NOTARYTOOL_PROFILE:-}" ]]; then
    printf 'Notarization requires DEVELOPER_ID_IDENTITY.\n' >&2
    exit 1
  fi
  codesign --force --deep --sign - "$APP"
  printf 'Development signature only: Developer ID and notarization are not configured.\n'
fi
codesign --verify --deep --strict "$APP"
lipo "$APP/Contents/MacOS/FontPocket" -verify_arch arm64 x86_64
ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUT/FontPocket-macOS-$VERSION-universal.zip"
if [[ -n "${NOTARYTOOL_PROFILE:-}" ]]; then
  xcrun notarytool submit "$OUT/FontPocket-macOS-$VERSION-universal.zip" --keychain-profile "$NOTARYTOOL_PROFILE" --wait
  xcrun stapler staple "$APP"
  xcrun stapler validate "$APP"
  ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUT/FontPocket-macOS-$VERSION-universal.zip"
fi
(cd "$OUT" && shasum -a 256 "FontPocket-macOS-$VERSION-universal.zip" > "FontPocket-macOS-$VERSION-universal.zip.sha256")
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
ditto "$APP" "$STAGE/Font Pocket.app"
ln -s /Applications "$STAGE/Applications"
cat > "$STAGE/Install.txt" <<'INSTALL'
Drag Font Pocket.app to Applications, then open it from Applications.
Font Pocket.app을 Applications 폴더로 드래그한 뒤 응용 프로그램에서 실행하세요.

Fonts and personal data are kept when replacing the app.
앱을 교체해도 설치한 폰트와 개인 데이터는 유지됩니다.
INSTALL
hdiutil create -volname "Font Pocket" -srcfolder "$STAGE" -ov -format UDZO "$OUT/FontPocket-macOS-$VERSION-universal.dmg"
hdiutil verify "$OUT/FontPocket-macOS-$VERSION-universal.dmg"
if [[ -n "${NOTARYTOOL_PROFILE:-}" ]]; then
  xcrun notarytool submit "$OUT/FontPocket-macOS-$VERSION-universal.dmg" --keychain-profile "$NOTARYTOOL_PROFILE" --wait
  xcrun stapler staple "$OUT/FontPocket-macOS-$VERSION-universal.dmg"
  xcrun stapler validate "$OUT/FontPocket-macOS-$VERSION-universal.dmg"
fi
(cd "$OUT" && shasum -a 256 "FontPocket-macOS-$VERSION-universal.dmg" > "FontPocket-macOS-$VERSION-universal.dmg.sha256")
printf 'Built %s\n' "$APP"

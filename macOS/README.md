# Font Pocket for macOS — 0.1.0 Preview

Native SwiftUI application for macOS 13 Ventura and later. The universal app supports Apple Silicon and Intel Macs. The original Font Pocket logo is preserved; interface icons use Apple's built-in SF Symbols.

## Build

Install Apple's Xcode Command Line Tools, then run:

```sh
cd macOS
swift test
bash build.sh
```

The app and ZIP are written to `macOS/dist`. GitHub Actions also builds a downloadable universal preview artifact on the `codex/macos-native` branch. Tests that read actual fonts use `FONTPOCKET_TEST_FONT` pointing to a licensed TTF file (CI uses the project's OFL-licensed Pretendard fixture).

## Included

- Batch download desktop fonts from Noonnu, Google Fonts, creator download pages and direct HTTPS font/ZIP URLs. Choose the actual files before downloading.
- Import TTF, OTF, TTC, OTC, ZIP archives and folders of fonts. Drag files into the window or use Open With.
- Read metadata, edit preview text and size, compare up to four fonts, search and favorite fonts.
- Install for the current macOS user, enable/disable app-owned fonts, and move them to Trash after confirmation.
- Back up selected font files to ZIP, cancel a running operation, and confirm before quitting during work. Cancelled backups preserve an existing ZIP.
- Korean and English UI, including settings and dialogs.

## Storage and safety

Installed files are copied to `~/Library/Fonts/FontPocket-<SHA256>.<extension>`. The ownership catalog and disabled fonts live in `~/Library/Application Support/Font Pocket/`. Files that were modified outside the app are refused by management operations. System and pre-existing fonts are read-only in Font Pocket. Changes may require other applications to refresh their font lists or restart.

ZIP imports reject traversal paths, symlinks, excessive expansion, encrypted archives, ZIP64 and path-override metadata. Import limit: 2,048 files, 200 MB compressed ZIP and 512 MB expanded data. Keep source filenames and licensing information when distributing fonts; Font Pocket does not grant font redistribution rights.

## Preview limitations

This preview has an ad-hoc signature, **not** an Apple Developer ID signature or notarization. Public distribution with normal Gatekeeper trust requires a separately configured Apple Developer account and notarization. No Windows signing certificate is reused. Do not disable Gatekeeper to run it.

WOFF/WOFF2 conversion, variable-font axis editing, character maps, font-version history, automatic app updates and Windows environment-backup compatibility are not implemented in this macOS preview. Use desktop TTF/OTF downloads. The app is distributed outside the App Store because it manages fonts in the user's font directory; it is not a sandboxed Store build.

## Testing

Tests cover URL parsing, ZIP path/symlink/size validation, real Core Text font metadata, queue deduplication, installation rollback, activation persistence, external-file change protection, ZIP backup/restore and cancellation. Repository tests inject temporary font folders and a registration stub, so they never modify your installed fonts. An actual Mac must still be used to verify user-wide Core Text registration and Finder/UI behavior before a public release.

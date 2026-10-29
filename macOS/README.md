# Font Pocket for macOS — 0.2.1

[한국어](README.ko.md) · [Windows → macOS feature map](FEATURE_PARITY.md)

Native SwiftUI font downloader and manager for macOS 13 Ventura and later. The universal app supports Apple Silicon and Intel. The original blue folder logo and Font Pocket wordmark are retained. Interface icons use Apple's system SF Symbols.

## Open

Download a successful **Build Font Pocket for macOS** artifact from the **codex/macos-native** branch. Extract the artifact, then extract **FontPocket-macOS-0.2.1-universal.zip** in Finder and open **Font Pocket.app**. ZIP preview inside a chat/editor does not launch an app.

This development build has an ad-hoc code signature. Apple Developer ID signing and notarization have not been performed.

## Included

- Batch download from Noonnu, Google Fonts, creator pages and direct HTTPS font/ZIP links, with file selection.
- Import files, folders and ZIPs together; TTF/OTF/TTC/OTC, WOFF/WOFF2/SFNT conversion; format/static/variable preferences and removable queue rows.
- Inline sample text/size, comparison up to 12 faces/256 MB, variable axis sliders, searchable Unicode character map and actual character coverage.
- Name, designer, manufacturer, version, copyright and license metadata; table bounds/checksum inspection.
- Name/file/version/note/tag search; format, activation, script, weight, static/variable, favorites and collection filters; family ordering.
- Notes, tags, favorites and editable manual/smart collections; select filtered fonts for collection activation.
- SHA-256 version archive, uninstalled versions, comparison and switching with rollback. Existing conflicting styles remain active while new files are archived.
- Exact-file and same-style duplicate review, keep-file choice, recovery ZIP before Trash and restore through the installation queue.
- Current-user install and activation. Existing user-folder fonts can be explicitly adopted after archiving originals. System/shared fonts remain protected.
- Selected-font ZIP and environment backup/restore: user-folder fonts, disabled managed fonts, versions, notes, tags, favorites, collections and activation. Existing data wins; conflicting active styles are archived.
- List/two-column/grid/family catalogs, multiple-size and full-character pagination, native print/PDF.
- Original-font or WOFF2 packages with CSS/editable HTML, standalone WOFF2.
- Command-K and global Command-Option-Space search, Finder reveal, Font Book, Open With, drag/drop, persistent history, cancellation and quit confirmation.
- Korean/English, optional daily update checks and official macOS update verification/restart/rollback.

## Apple Liquid Glass

On macOS 26+, native NavigationSplitView/toolbars, real glass/glassProminent styles, GlassEffectContainer and glassEffect are used for controls. Font lists and previews keep readable backgrounds. Older macOS uses native controls/material. System reduced-transparency/motion settings are respected. Xcode 26+ includes the new APIs; older compilers build the native fallback.

[Apple Liquid Glass](https://developer.apple.com/kr/documentation/technologyoverviews/liquid-glass) · [Adopting Liquid Glass](https://developer.apple.com/kr/documentation/technologyoverviews/adopting-liquid-glass)

## Build and verification

On a Mac with Xcode Command Line Tools:

    cd macOS
    swift test
    bash build.sh

Outputs are in macOS/dist. Tests use FONTPOCKET_TEST_FONT pointing to a licensed TTF; CI uses the project's OFL Pretendard fixture. CI tests macOS 14 compatibility and macOS 26, builds both architectures, verifies the packaged signature and launches the native app in isolated font/catalog folders. Smoke checks exercise Core Text activation, packaged WOFF2 conversion, environment backup, personal data, catalog PDFs and native windows. Hosted Macs are used; this does not install the app on the owner's MacBook.

## Storage and limits

Active managed files: ~/Library/Fonts/FontPocket-<SHA256>.<extension>. Catalog, disabled fonts, originals, personal data, recovery ZIPs and versions: ~/Library/Application Support/Font Pocket/. Management refuses externally modified files.

Downloads/ZIPs: 1 GB compressed, 2 GB expanded, 10,000 entries. Native fonts: 512 MB; WOFF2 input: 128 MB, converted output: 150 MB. Comparison: 12 faces/256 MB. Printing: 300 pages with an explicit error before printing if exceeded. ZIP64, encryption, symlinks, traversal/override filenames and excessive expansion are rejected. Created backups must pass the restore policy before replacing an existing file.

Windows registry/associations are replaced by Core Text, Finder and Font Book. Environment ZIP formats are platform-specific; ordinary font ZIPs can be imported on both platforms. In-app updates require an official macOS ZIP and matching SHA-256 sidecar. Font redistribution still depends on the creator's license.

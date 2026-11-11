# Font Pocket for macOS 0.3.0

This release candidate was audited against the current Windows 3.1.18 usage guide and action menus. Native macOS controls and Apple Liquid Glass remain in place.

- Links: queue or download-and-install, multiple sources, format/static/variable preferences, file picker and direct HTTPS links; an omitted `https://` is accepted.
- Details: switch between same-family files/styles in weight order; reset character maps, analysis and variation axes when switching. Table tags describe the selected TTC face.
- Quick search: keyboard-focused search, details, activation, manager navigation and filename matching.
- Catalog: editable size, four layouts, current-size / multiple-size / complete-character printing and PDF. Double-click opens font details.
- Manager: native right-click row targeting, clear selection, duplicate-only filter, reset filters, direct collections/versions/catalog/inspection actions and collection-wide activation.
- Import: malformed files in a mixed folder/ZIP are reported while valid fonts stay in the queue. Empty folders are reported. Copies are checked against the source digest.
- Backup: hidden selected fonts remain in the backup, and installation from font backups is available.
- Restore: validate the complete archive before mutation, report actual processed items and preserve metadata for completed files when stopped or interrupted. Existing personal edits win.
- Versions: refresh the vault after operations; protect unrelated styles in TTC/OTC replacements and external/system fonts. Comparison consistently enforces the 12-face / 256 MB limits.
- Updates: choose the highest stable macOS version with its matching checksum, regardless of release or asset order. Reject partial filename matches and foreign asset sources. Manual updates are available in Settings.
- Lifecycle: one main window owns the library; Stop escalates if a native helper ignores termination.
- Distribution: universal app ZIP, DMG with Applications shortcut, checksums, and optional Developer ID / notarization support on a configured release Mac.

The generated CI package is ad-hoc signed. Developer ID/notarization and an actual public-release updater replacement require the owner's signing/release environment; these are not represented as completed by automated tests.

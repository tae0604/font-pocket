# Windows → macOS feature map (0.2.2)

This map follows the Windows 0.1.4 usage guide. UI placement follows macOS conventions.

| Windows capability | macOS implementation |
| --- | --- |
| Bulk link downloads, source/file selection | Install fonts; Noonnu, Google Fonts, direct HTTPS and creator/Font Library pages; bounded traversal, related-page picker |
| Files, ZIPs, folders, TTF/OTF/TTC, WOFF/WOFF2 | Shared queue; native WOFF decode, bundled WebAssembly WOFF2 codec; SFNT import; static/variable and format preferences |
| Inline preview and comparison | Shared text/size, up to 12 faces / 256 MB, details and archive-version comparison |
| Variable axes | Native Core Text sliders in Details → Preview |
| Unicode character map | Search code point/character, range filter, pagination, copy |
| Supported scripts / characters | Actual Core Text coverage and counts |
| Font metadata and inspection | Version, designer, manufacturer, copyright/license; table bounds/checksums and Core Text parsing |
| Search and advanced filters | Name, filename, version, note/tag, format, state, script, weight, static/variable, favorites, collection, family order |
| Notes, tags, favorites | Persistent personal library JSON and environment backup |
| Manual / smart collections | Create/edit/delete; per-font membership; filter and select visible results for batch activation |
| Duplicate cleanup | Exact SHA-256 vs same-style candidates, keep-file choice, protected system files, recovery ZIP |
| Version vault | Installed/uninstalled file capture, hashes, comparison, activation and rollback; conflicting active styles preserved |
| User-font install / activation / deletion | Core Text user scope; managed files; explicit adoption of existing user-folder fonts with original backup; Trash |
| Deleted-font restore | Recovery ZIP to installation queue |
| Font ZIP backup | Selected files, atomic replacement and cancellation; ordinary font ZIPs work across platforms |
| Full environment backup / restore | User-folder and disabled managed fonts, personal data, collections, version vault and activation; integrity first, existing data wins |
| Catalog layouts and print/PDF | List, two columns, grid, family; multiple sizes and all characters; pagination and native print/PDF |
| Webfont / CSS / HTML export | Original or WOFF2 package, editable HTML, standalone WOFF2 |
| Quick search / global shortcut | Command-K / Command-Option-Space; native focused search sheet |
| File location / system font UI | Finder / Font Book |
| File association / drag-drop | Finder Open With and window drop handler |
| Updates | Optional daily check; official repository, macOS ZIP + checksum, code-signature/bundle/version verification, restart/rollback |
| History, stop, busy quit warning | Persistent history; task cancellation; native confirmation and cleanup before exit |
| Localization / app-wide design | Korean/English, native sheets/settings; real Apple Liquid Glass on macOS 26 and native older-system fallback |

## Platform boundaries

- Windows registry entries, Windows print drivers and administrator-wide installation are OS-specific. The Mac app uses current-user Core Text, Finder associations, Font Book and the macOS print panel.
- Windows and Mac full-environment ZIP manifests are distinct. Font ZIP backups are portable.
- File inspection validates font structure/checksums and Core Text readability; it is not an antivirus or a guarantee for every renderer.
- Developer ID/notarization requires Apple signing credentials. This development artifact uses ad-hoc signing.
- The updater can be exercised against a newer official Mac release after that release and its checksum are published. It does not reuse Windows installers.
- CI builds run on hosted Macs; no claim is made that this installs or runs on the owner's MacBook.

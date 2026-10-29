# macOS 0.2.1 — fixes and performance review

The Windows implementation is unchanged. Existing Mac catalog/personal/environment formats remain compatible.

| Reproduction / finding | Fix |
| --- | --- |
| Korean/English tab labels shrink to ellipses on macOS 26 | Native segmented controls receive measured text widths. The strip scrolls horizontally when necessary. Only the selected pane is constructed. |
| Every character-grid redraw reopens the same font and creates hundreds of CTFonts | Reuse immutable Core Text font sources and a bounded cache of sizes. Reuse metadata while file size/date/inode are unchanged. |
| File size remains stale after replacing a file at the same URL | Read fresh filesystem attributes instead of Foundation's cached URL resource values. Cache invalidation is regression-tested. |
| Copying a glyph / changing pages repeatedly filters the whole font | Build a character index once, cache coverage counts, and filter only when the query/range changes, off the UI actor with cancellation. Literal character queries no longer also match hexadecimal labels. |
| Large WOFF reads/zlib conversion block the main actor | Move file reads and WOFF decoding off the UI actor. Check size before reading/copying. Keep only WebKit calls on the UI actor. |
| Cancelling a version switch can also cancel rollback | Restoration of the original activation state explicitly ignores cancellation; the real cancelled-task case is tested. |
| A removed managed font cannot be reinstalled because its catalog entry remains | Recreate a missing file from a verified source while preserving its activation state. Refuse to overwrite changed existing files. |
| An archive reports success even when the indexed original is missing/corrupt | Verify the stored version; an explicit recapture atomically repairs the original. |
| Multiple same-style files in one install batch use a stale installed-font snapshot | Track active names as each file commits. Preserve conflicting versions in the vault. |
| Install loses manual collection membership / partial adoption loses notes | Remap notes and collection IDs together; persist each committed file and refresh after failure/cancellation. |
| Overlapping duplicate groups can delete another group's keep choice | Protect the union of all selected keep files and deduplicate deletion candidates. |
| A long-open editor overwrites restored favorites/collections | Merge only edited fields against the editor's baseline. Reject conflicting edits without replacing saved data. |
| Closing the editor or quitting drops unsaved changes | Offer Save / Discard / Cancel and an unsaved-edit quit warning. |
| A malformed later environment record leaves earlier fonts installed | Validate every filename/format/hash before mutations. Maintain active-name state during restore and install disabled fonts without transient registration. |
| Large/shared TTC tables are checksummed repeatedly and stop responds slowly | Read the inspection buffer once, reuse shared-table checks and check cancellation inside long checksum loops. |

Regression coverage includes real font metadata/cache invalidation, literal/Unicode search, cancelled rollback, missing-file repair, changed-file refusal, vault repair, personal-data merge/conflicts, overlapping duplicate keep choices, membership remapping and pre-mutation restore rejection. Native smoke checks inspect the measured label widths in both languages and render light/dark screenshots.

No claim is made that every possible font, site, printer or OS configuration is bug-free. Build/test evidence is included with the packaged artifact.

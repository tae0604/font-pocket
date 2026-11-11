# macOS release procedure

## Reproducible build

Run from this folder on a Mac with Xcode 26 or later selected:

    export FONTPOCKET_TEST_FONT="$PWD/../outputs/FontPocket/Brand/Fonts/Pretendard-SemiBold.ttf"
    export FONTPOCKET_LIVE_CORETEXT_TEST=1
    swift test
    bash build.sh

The build produces a universal arm64/x86_64 application, ZIP, DMG and SHA-256 sidecars in `dist/`. The DMG contains the app, an Applications shortcut and installation instructions. Install by dragging the app to Applications. App replacement keeps installed fonts, personal metadata and archived versions.

The CI workflow tests the native fallback on macOS 15 and Liquid Glass on macOS 26, exercises real user-scope Core Text registration and launch/installation flows, and validates the universal binary and disk image. macOS 13 remains the declared deployment target; macOS 13/14 and a physical Intel Mac still require manual release-machine testing. macOS 14 GitHub runners were retired on November 2, 2026 ([GitHub announcement](https://github.blog/changelog/2026-10-01-github-actions-macos-14-runner-image-retirement/)).

## Public distribution signing

The default build is ad-hoc signed and **not notarized**. To produce the signed distribution on a release Mac, first install the owner's existing Developer ID certificate/private key and create a `notarytool` keychain profile using Apple's documented process. Then set:

    export DEVELOPER_ID_IDENTITY='Developer ID Application: Your Name (TEAMID)'
    export NOTARYTOOL_PROFILE='your-existing-keychain-profile'
    bash build.sh

The script signs with hardened runtime, submits the app ZIP and DMG, staples tickets and verifies them. A failure stops the build. The script does not create accounts, purchase certificates or collect credentials. CI does not set these variables and must not be described as a notarized release.

## Before publishing

Confirm the ZIP and DMG open from a normal browser download on the release Mac, drag the app to Applications, restart it, and verify existing library data remains. Test the selected display language, light/dark appearance, reduced transparency, large display scaling and the OS font UI. Check the app's Gatekeeper assessment for the **signed** distribution.

Publish the exact versioned ZIP with its `.zip.sha256` sidecar; the in-app updater uses this pair. The DMG and `.dmg.sha256` are for manual installation. A newer official macOS release is needed to validate a real public update, replacement, relaunch and rollback end to end. Windows installers and full environment-backup manifests are not Mac update/install packages.

Tests verify font structure, checksums, lifecycle and selected UI flows. They cannot certify all third-party fonts or every renderer. Keep the detailed CI record and feature parity document with the release.

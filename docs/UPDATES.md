# In-app updates

Installed copies can check public GitHub Releases at startup, or manually from About > Updates. Startup checks can be disabled there. PolyPDF sends no documents or account tokens; it requests release metadata and the selected public assets from robertmartic/polypdf.

Choose Update now to download and verify an update, or Later to keep using the current version. Downloads can be cancelled before installation. Unsaved documents use the normal Save / Don't save / Cancel decisions before restarting. Other PolyPDF instances must also be closed.

## Initial baseline and future updates

The 0.1.0 release is a full baseline with no delta from the retired development versions. Later releases may publish differential packages based on 0.1.0 or another published version in the new sequence. Older installations use the full-package fallback when no matching delta exists. Moving from the retired 0.2.x–0.5.0 sequence to 0.1.0 requires the one-time uninstall/reinstall procedure in INSTALLATION.md; downgrade checks are not bypassed.

## File-level differential packages

The manifest lists each program file, its size and SHA-256. A delta is used only when its base version and every installed base-file hash match. It contains whole new/changed files; it is not a patch of individual byte ranges. Unchanged .NET runtime and PDFium files need not be downloaded again. If the base is unavailable or modified, the full portable package is used instead.

The manifest and a small maintenance installer are downloaded alongside the payload. The maintenance installer refreshes the Windows version registration and uninstaller without including the large program payload. All downloaded assets are checked against the SHA-256 digests returned by the official GitHub release API over HTTPS. This trusts the project's GitHub account and release service; packages are not code-signed yet.

The helper runs from a separate local copy, waits for the app to exit, copies the existing installation into a sibling staging folder and applies the update there. It verifies every target file before switching folders. On a replacement or registration error it restores the old folder. Preferences and recovery data remain in the user's separate PolyPDF data folder. Unrelated files in the installation directory are preserved.

The previous installation is retained in a sibling `.PolyPDF-backup-*` folder for recovery. Failed staging/installation folders and helper/download caches may also remain for diagnosis. A power loss or forced termination during the directory switch is not the same as a handled error; retain the backup and rerun the official full installer if needed. Do not store personal PDFs in a program folder.

After successful restart, About opens its embedded Changelog once. Update preferences and the latest result are in `%LOCALAPPDATA%\PolyPDF`; update downloads and helper copies are under its Updates folder. A failed update is never marked successful.

## Progress, retry and storage

Failed or cancelled downloads can be retried from the same update window. Connection failures, timeouts, rate limits and verification failures show actionable messages. Progress distinguishes release information, changed/full files, installer download, verification, staging, installation and restart. A successful file replacement followed by a restart failure is reported as installed; open the existing shortcut in that case.

Before downloading the payload, PolyPDF checks free space for the download/helper cache and a conservative staging allowance, combining requirements if both are on the same drive. Space is checked again before staging. There is a 64 MB safety reserve; space can still change afterward, so normal rollback remains necessary.

About > Updates > Manage update storage shows recognised downloads and backups, available cleanup space and retention reasons. Cleanup is explicit. Ended downloads/helper copies become eligible after 7 days; older recovery backups after 30 days. The newest verified backup and backups referenced by the latest results are retained. Active/prepared jobs and folders with additional files are retained. Cleanup and update preparation/application share an exclusive lock.

Folders without ownership metadata, linked folders, malformed metadata and failed staging folders are deliberately left for manual review. They are not included in managed storage totals. Settings, document recovery copies and personal PDFs are outside cleanup. Interrupted preparing/applying jobs remain protected rather than being guessed safe to delete.

An isolated regression test terminates a test helper after directory replacement, verifies the original backup and absence of a success result, and restores the fixture from that backup. This covers forced termination at that point, not every possible power-loss timing. For a real interrupted installation, retain sibling backup/failed folders and use the official full installer if the shortcut cannot open PolyPDF. Do not delete recovery copies until the working app and documents are confirmed.

## Release assets and automation

Publish the conventional setup, portable ZIP, source ZIP, `PolyPDF-VERSION-manifest.json`, and `PolyPDF-VERSION-maintenance.exe`. When building against an earlier release manifest, also publish `PolyPDF-BASE-to-VERSION-delta.zip`. Publish source and verify every asset before making the release public. See AGENTS.md for the agreed release workflow.

Build with `tools/publish.ps1 -BaseManifest PATH` (omit the base for the initial updater release), then run `tools/build-installer.ps1 -SkipPublish` both normally and with `-MaintenanceOnly`. The source package contains these scripts; generated manifests/packages remain outside source history.

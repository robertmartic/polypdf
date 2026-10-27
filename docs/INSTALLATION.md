# Install PolyPDF on Windows

PolyPDF provides a conventional per-user installer. It does not require administrator privileges. Windows 10 version 2004 or newer / Windows 11 x64 is required.

## Install

1. Close every PolyPDF window.
2. Run `PolyPDF-0.3.0-setup.exe`.
3. Choose desktop and Start menu shortcuts on the components page.
4. Choose a new, empty folder, normally `%LOCALAPPDATA%\Programs\PolyPDF`, then install.
5. Launch from the selected shortcut or the installer's optional launch checkbox.

The installer registers PolyPDF in Windows installed apps and in Open with. It does not change the default PDF application. To select a default, use Windows Settings or Open with > Choose another app.

## Reinstall or upgrade

Close PolyPDF and run the same or newer installer. The registered folder and shortcut choices are reused. An older version is rejected. To move the application, uninstall first, then install into the new folder; preferences are preserved.

An existing portable/copy-only installation without an installer marker is not adopted automatically. Choose a new, empty folder for a conventional installation. Keep your PDFs outside the program folder.

Installed copies can use About > Updates and optional startup checks. See UPDATES.md for file-level differential packages, verification, staging and rollback.

## One-time transition to the 0.1.0 baseline

Earlier development installers numbered 0.2.x through 0.5.0 compare as newer than the reset 0.1.0 baseline. The updater and installer intentionally retain their downgrade protection. Download and verify the current setup before removing such a development build. Close PolyPDF and copy %LOCALAPPDATA%\PolyPDF to a private backup location; recovery data remains tied to the current Windows user and machine. Uninstall the earlier development build through Windows installed apps, then install PolyPDF-0.3.0-setup.exe into the normal empty program folder and select your shortcut choices. Normal upgrades from the published 0.2.0 and later line update in place and preserve settings, document recovery, installation location and shortcut choices.

## Uninstall

Use Windows Settings > Apps > Installed apps > PolyPDF > Uninstall, or run `Uninstall.exe` in the installed folder. Close PolyPDF first.

Only installer-owned program files, its shortcuts and its own registration are removed. Unrelated files placed in the program folder are retained. Settings at `%LOCALAPPDATA%\PolyPDF\settings.json`, recovery copies at `%LOCALAPPDATA%\PolyPDF\Recovery` and personal PDFs are retained. If an unrelated file prevents removal of the now-empty program folder, the folder remains.

The installer is currently unsigned. Windows may show an unknown-publisher/reputation prompt. Use only packages from the project's approved release or your own verified build. Code signing is separate future release work.

## Build setup

From a Windows source checkout, after `tools/bootstrap.ps1`:

```powershell
powershell -ExecutionPolicy Bypass -File tools/bootstrap-installer.ps1
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1
```

The NSIS 3.13 compiler is downloaded into `tmp/installer-tools` and checked against the pinned SHA-256 hash. It is not installed globally and is not committed to source. The generated setup is in `dist/` and uses zlib compression. NSIS notices are in `vendor/licenses/nsis/` and included in the application payload.

For developer lifecycle checks, build with `-TestIdentity -SkipPublish` after publishing the app, then run `tools/test-installer.ps1`. This build uses separate registry keys and shortcut names, and installs only inside the workspace's temporary test directory. It is not a public installer.

Silent deployment supports `/S`, `/DESKTOP=0` or `1`, `/STARTMENU=0` or `1`, and `/D=absolute path` as the final command-line option. With NSIS, the `/D` value is not enclosed in quotes even when the path contains spaces. An installer validation failure returns exit code 2. Silent mode does not launch the application automatically.

Rejected setup/uninstall operations write the reason to `%TEMP%\PolyPDF-setup-error.txt` (the test build uses `PolyPDF.InstallerTests-setup-error.txt`).

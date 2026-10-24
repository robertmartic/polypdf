# PolyPDF

A free, open-source PDF workspace for Windows 10 (version 2004 or newer) and Windows 11, x64. C# / .NET 10 / WPF. English interface, light and dark themes, local document processing, no accounts or subscription.

## Run the finished app

Current development version: **0.1.1 (pre-1.0)**. Browse the published source at [robertmartic/polypdf](https://github.com/robertmartic/polypdf) and published packages at [GitHub Releases](https://github.com/robertmartic/polypdf/releases). A local build is not automatically a published release.

Run `PolyPDF-0.1.1-setup.exe` for a conventional installation with a folder choice, optional desktop/Start menu shortcuts, Windows installed-app registration, Open with integration and an uninstaller. No administrator privileges or separate .NET installation are required. Close PolyPDF before setup. See [installation instructions](docs/INSTALLATION.md).

For portable use, extract `PolyPDF-0.1.1-win-x64.zip`, then open `PolyPDF/PolyPDF.exe`. Keep the complete folder together. In a developer workspace, the prepared executable is at `dist/PolyPDF/PolyPDF.exe`. The `PolyPDF-0.1.1-source.zip` package contains source code and build tools; follow Build from source below to build it.

The current version is shown below the PolyPDF name, in the window title, and in About. All three read the compiled assembly version, set by the project's Version property.

Version 0.1.1 introduces the new red PolyPDF icon throughout the application and Windows integration. The initial 0.1.0 baseline includes supported text movement, annotations, page organisation, local OCR, encrypted recovery, conventional installation and in-app updates. Release notes are embedded in About > Changelog and can be read offline. Fit Width and Fit Height follow window resizing, page navigation and rotation; manual zoom exits fitting mode. Windows-owned file and printer pickers retain the operating system's appearance. See ROADMAP.md for subsequent milestones.

The conventional installer replaces the earlier copy-only `Install.cmd` workflow. It preserves the registered installation folder and shortcut choices on reinstall, rejects downgrades, and never forces the default PDF association. To choose PolyPDF as the default, use Windows Settings > Apps > Default apps, or Open with > Choose another app.

To uninstall an installed copy, close PolyPDF and use its entry in Windows installed apps or `Uninstall.exe` in the application folder. Preferences in `%LOCALAPPDATA%/PolyPDF/settings.json`, encrypted recovery copies in `%LOCALAPPDATA%/PolyPDF/Recovery`, and personal documents are preserved. Portable folders can simply be removed after closing the app. Installed copies now support optional startup checks and manual checks under About > Updates. See docs/UPDATES.md for differential downloads, verification and rollback.

## Moving from the earlier development builds

The new official version sequence starts at 0.1.0 and retains the existing feature set. Earlier development builds numbered 0.2.x through 0.5.0 belong to the previous sequence. They cannot update down to 0.1.0: first download the verified 0.1.0 setup, close PolyPDF, back up your local settings/recovery folder, uninstall the earlier build normally, then install 0.1.0. The uninstaller preserves settings, recovery data and personal documents. See [installation instructions](docs/INSTALLATION.md). Subsequent newer releases use the in-app updater.

## Everyday tools

- Multiple document tabs, drag-and-drop, recent files, thumbnails, Fit Width, Fit Height and zoom.
- Persistent active tool-group indicator and embedded release history in About > Changelog.
- Select text by dragging a rectangle. Copy the selection with Ctrl+C or use Copy page text.
- Search text with Ctrl+F and Find next. Results wrap through the document.
- Add multiline text with Segoe UI, Arial or Times New Roman, font size and colour.
- Select and move supported PolyPDF-added text with Annotate > Move text. New multiline insertions move together; older additions move as individual lines. Drag the frame and release to place the text, or press Escape to cancel. Undo/Redo applies to each completed move.
- Edit a supported existing text object, keeping its font and position.
- Highlight selected text, draw freehand and add PDF comments. Use View > Comments to read notes on the current page.
- Rotate, reorder, insert and delete pages. Extract page ranges and merge PDFs.
- Undo and redo document edits. History is kept in memory (up to 25 revisions, with a 96 MB target budget; a single large revision can exceed it).
- Save, Save as, export a page to PNG, and print the document.
- Offline OCR of the current page using Windows OCR, with editable language code and copyable results.
- Automatic encrypted recovery checkpoints for unsaved work, with restoration offered after an interrupted session.

## Recovery and reliability

Unsaved documents receive a recovery checkpoint after three seconds of inactivity following an edit and are checked again every 30 seconds. Checkpointing waits while another operation is running. Edits since the last completed checkpoint may be lost if the process is interrupted before the next checkpoint finishes. Originals are never overwritten by recovery.

At the next start, choose **Yes** to recover, **No** to discard the discovered copies, or **Cancel** to keep them for a later start. Recovered documents open as unsaved files named `(recovered).pdf`; Save asks for a destination. Copies are removed after saving or explicitly closing without saving. If a copy cannot be read, it is retained for another attempt.

Recovery files, including their metadata, are encrypted with Windows DPAPI for the current Windows user. They are local to that user and machine; moving them to another computer or account is not a supported backup strategy. Active app instances hold separate session leases and are skipped by recovery discovery. PDF passwords are entered in a masked PasswordBox and are not saved in recovery data.

PDF rendering, search, editing, saving and history serialisation use a dedicated PDF thread. PDFium is never called concurrently. Thumbnails load asynchronously as they become visible, and zoom/resize rendering is debounced. Controls for document operations pause while work is running; the window remains responsive. Print spooling uses a separate STA thread.

Undo/Redo tracks the saved revision, including branches after Undo. Added text reuses fonts within the document, including after save/reopen and Undo. View, Pages, search and document switching return to text selection rather than leaving a hidden drawing tool active.

## Practical limits

Existing text editing is intentionally limited to top-level, unskewed and unrotated text objects. A PDF may split one word into several objects. Embedded subset fonts may not contain replacement characters; unsupported replacements roll back. Text does not automatically reflow, and longer replacements may overlap nearby content. It is not a full word processor. Scanned text requires OCR and cannot be directly edited as a text object.

Move text supports top-level text objects carrying PolyPDF insertion marks. It does not move original third-party text, scans, images, comments, highlights or drawing strokes. Other PDF editors may flatten or discard these marks, preventing later selection. Formatting and resizing selected objects are planned for later versions.

OCR requires the matching Windows OCR language pack, such as `en-US` or `hr-HR`. Install it in Windows Settings > Time & language > Language & region > Language options. Availability depends on the Windows installation and language. OCR returns text for copying; it does not yet create a searchable PDF text layer. Recognition can contain errors.

PDF forms are displayed but filling interactive fields is not supported yet. Existing comments can be read; modifying or deleting existing annotations is not included yet. Images can be viewed but not edited. The print command prints all pages, scaled to the selected printer's printable area. No cryptographic signing or redaction tools are included.

Password-protected documents can be opened with a valid password. Saved copies are not encrypted; PolyPDF does not create or preserve password protection. Signed PDFs show a warning before editing; changed documents do not preserve signature validity. Respect the original document's restrictions. Keep originals when working with protected or signed files.

Documents are read into memory. Large files, page previews and full-document undo snapshots can use substantial memory. Raster previews are capped at 6000 pixels per dimension. Long native operations are serialised on the PDF worker, so additional document actions wait until the current operation completes. Cancelling a running native operation is not yet supported. Physical printer output has not been verified in this development environment.

## Shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+O | Open PDFs |
| Ctrl+S / Ctrl+Shift+S | Save / Save as |
| Ctrl+F | Search |
| Ctrl+C | Copy selected text |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+P | Print |
| Ctrl+W | Close current document |
| Ctrl+mouse wheel | Zoom |
| Escape | Return to text selection |

## Build from source

Requirements: Windows 10 version 2004 or newer / Windows 11 x64, .NET 10 SDK with Windows desktop build support, PowerShell, Windows `tar`, and internet access for the first dependency download and .NET restore. Run the commands from the extracted source folder containing `PolyPDF.csproj`. A newer installed SDK may select different servicing versions of the .NET runtime; the tested release runtime is recorded below.

```powershell
powershell -ExecutionPolicy Bypass -File tools/bootstrap.ps1
dotnet build PolyPDF.csproj -c Release
dotnet run --project PolyPDF.csproj -c Release
```

PDFium is pinned to `bblanchon.PDFium.Win32` version `151.0.7920`. Self-contained packages include .NET 10; its servicing version can vary with the SDK patch selected by `global.json`. The SDK and runtime versions are recorded in the CI build report. The bootstrap downloads the native DLL, headers and full upstream license collection. No paid PDF SDK is used. Font files are read from the installed Windows system; fonts themselves are not shipped with the app.

`tools/bootstrap.ps1` checks SHA-256 hashes of the pinned NuGet package and corresponding PDFium release archive before extraction. The expected hashes are recorded in the script. Do not replace a hash merely to bypass a failed download check; dependency changes need a reviewed version and matching license notices.

Create a self-contained portable distribution:

```powershell
powershell -ExecutionPolicy Bypass -File tools/publish.ps1
```

To also build the conventional installer, run `tools/bootstrap-installer.ps1` once and `tools/build-installer.ps1 -SkipPublish` after publishing. This uses a hash-pinned, portable NSIS 3.13 compiler in the workspace; no global build-tool installation is needed. See docs/INSTALLATION.md for isolated installer lifecycle tests.

Run the integration checks without displaying a window:

```powershell
Start-Process bin/Release/net10.0-windows10.0.19041.0/win-x64/PolyPDF.exe -ArgumentList '--self-test' -Wait
Get-Content bin/Release/net10.0-windows10.0.19041.0/win-x64/test-results/result.txt
```

The checks exercise actual native PDF operations: text and Unicode round trips, replacement, undo/redo, highlights, comments, drawing, page order, rotation coordinates, extracting, merging, deleting, transaction rollback, save/reopen and rendering. Regression checks verify saved revision tracking and branching, font reuse after Undo/reopen, all four page rotations, encrypted recovery, isolation from live sessions, recovery after an actual subprocess exit without cleanup, and retention of damaged recovery files.

Run the additional UI checks with the same executable and `--ui-test`, after `--self-test`. These check tool mode reset, dispatcher responsiveness, off-screen light/dark layouts, password masking, Fit Height on resize/navigation/rotation, active tool groups, embedded Changelog scrolling and English Windows OCR. UI checks require the `en-US` Windows OCR language pack and a Windows desktop session. Check the process exit code and `ui-result.txt`; failures are written to `ui-error.txt`. All results are in `test-results` beside the executable.

Optional independent PDF fixtures can be generated with `tools/create_test_fixtures.py` using Python with `reportlab` and `pypdf`. These are generated examples, including files with the test password `reader`, and are excluded from source packages. When present in `tests/fixtures`, `--self-test` additionally checks external-font editing and password/permission handling. The core checks do not require Python or these fixtures.

## Source packaging and contributions

Run `powershell -ExecutionPolicy Bypass -File tools/package-source.ps1` to create the source ZIP in `dist/`. It stages a fresh copy of source, selected documentation, assets, build tools and license notices. It excludes executables, dependency downloads, personal PDFs, generated test results and local settings. Review the staged file list before public publication; `.gitignore` does not remove files already tracked by Git.

See [CONTRIBUTING.md](CONTRIBUTING.md) for development checks, [ROADMAP.md](ROADMAP.md) for planned milestones, [CHANGELOG.md](CHANGELOG.md) for released changes, and [the GitHub publication plan](docs/GITHUB-PUBLISHING.md) for the next agreed step.

## License

PolyPDF source code is MIT licensed. PDFium and its dependencies retain their own licenses; full notices are included in the distribution. The self-contained build also includes Microsoft's .NET runtime license and notices. See LICENSE and THIRD-PARTY-NOTICES.txt.

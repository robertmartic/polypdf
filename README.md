# PolyPDF

**[Download PolyPDF for Windows](https://github.com/robertmartic/polypdf/releases/latest)** — under Assets choose the file ending in `-setup.exe`.

A PDF workspace for Windows 10 (version 2004 or newer) and Windows 11, x64. C# / .NET 10 / WPF. English interface, light and dark themes, local document processing, no accounts or subscription.

## Run the finished app

Current development version: **0.3.1 (pre-1.0)**. Download official packages from [GitHub Releases](https://github.com/robertmartic/polypdf/releases). Version 0.3.1 is the compatibility bridge to the signed PolyStudio update service. A local build is not automatically a published release.

Run `PolyPDF-0.3.1-setup.exe` for a conventional installation with a folder choice, optional desktop/Start menu shortcuts, Windows installed-app registration, Open with integration and an uninstaller. No administrator privileges or separate .NET installation are required. Close PolyPDF before setup. See [installation instructions](docs/INSTALLATION.md).

For portable use, extract `PolyPDF-0.3.1-win-x64.zip`, then open `PolyPDF/PolyPDF.exe`. Keep the complete folder together. In a developer workspace, the prepared executable is at `dist/PolyPDF/PolyPDF.exe`.

The current version is shown below the PolyPDF name, in the window title, and in About. All three read the compiled assembly version, set by the project's Version property.

Version 0.3.0 adds a whole-document Annotations panel and supported annotation editing/deletion to the compact vector toolbar workflow. The initial 0.1.0 baseline includes supported text movement, annotations, page organisation, local OCR, encrypted recovery, conventional installation and in-app updates. Release notes are embedded in About > Changelog and can be read offline. Fit Width and Fit Height follow window resizing, page navigation and rotation; manual zoom exits fitting mode. Windows-owned file and printer pickers retain the operating system's appearance. See ROADMAP.md for subsequent milestones.

The conventional installer replaces the earlier copy-only `Install.cmd` workflow. It preserves the registered installation folder and shortcut choices on reinstall, rejects downgrades, and never forces the default PDF association. To choose PolyPDF as the default, use Windows Settings > Apps > Default apps, or Open with > Choose another app.

To uninstall an installed copy, close PolyPDF and use its entry in Windows installed apps or `Uninstall.exe` in the application folder. Preferences in `%LOCALAPPDATA%/PolyPDF/settings.json`, encrypted recovery copies in `%LOCALAPPDATA%/PolyPDF/Recovery`, and personal documents are preserved. Portable folders can simply be removed after closing the app. Installed copies now support optional startup checks and manual checks under About > Updates. See docs/UPDATES.md for differential downloads, verification and rollback.

## Moving from the earlier development builds

The new official version sequence starts at 0.1.0 and retains the existing feature set. Earlier development builds numbered 0.2.x through 0.5.0 belong to the previous sequence. They cannot update down to 0.1.0: first download the verified 0.1.0 setup, close PolyPDF, back up your local settings/recovery folder, uninstall the earlier build normally, then install 0.1.0. The uninstaller preserves settings, recovery data and personal documents. See [installation instructions](docs/INSTALLATION.md). Subsequent newer releases use the in-app updater.

## Availability

All current tools are free to use, including for commercial work. No subscription, activation or account is required. Future optional Pro features may use a one-time purchase; there are no Pro restrictions in this release. Development from 0.1.2 is private. Earlier MIT releases retain their existing permissions.

## Opening and editing PDFs

Newly opened PDFs and document-tab switches use Fit Width. Fit Height remains available manually; manual zoom is retained during window resizing and page navigation. Use Annotate > Move text to select a PolyPDF-added block, then Text properties to edit its content or formatting, or Delete text to remove it. Empty clicks and Escape clear the selection. Changes to supported blocks are undoable; text does not automatically reflow.

## Everyday tools

- Multiple document tabs, drag-and-drop, recent files, thumbnails, Fit Width, Fit Height and zoom.
- Persistent active tool-group indicator and embedded release history in About > Changelog.
- Select text by dragging a rectangle. Copy the selection with Ctrl+C or use Copy page text.
- Search text with Ctrl+F and Find next. Results wrap through the document.
- Add multiline text with Segoe UI, Arial or Times New Roman, font size and colour.
- Inspect and edit supported added text through Annotate > Move text > Text properties, including actual font, size, colour and content.
- Select and move supported PolyPDF-added text with Annotate > Move text. New multiline insertions move together; older additions move as individual lines. Drag the frame and release to place the text, or press Escape to cancel. Undo/Redo applies to each completed move.
- Delete a selected PolyPDF-added block with Delete text, or Delete while the PDF page has keyboard focus. Undo restores the complete block. Original text and scans are excluded from this deletion tool.
- Use Edit original text for limited editing of supported original PDF objects. Use Move text > Text properties for PolyPDF-added text.
- Highlight selected text, draw freehand and add PDF comments. Use the comments icon in View or Annotate to open Annotations for the whole document. Filter comments, highlights or marked drawings; select an item to jump to it. Use Edit comment or Delete for supported items.
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

## Annotations workflow

Choose Annotate > Select annotation to pick a comment, highlight or marked stroke on the page. The Annotations panel lists items across the entire document, including unsupported annotations for reference. Selecting a list item navigates to its page and scrolls to its location. Edit supported note content with Apply/Cancel, or delete a supported selected item. Delete works only while the page or annotations list has focus; typing in a text field does not remove PDF content. Each completed edit/deletion is one Undo/Redo step. Empty page clicks and Escape clear the selection.

New freehand strokes created in 0.3.0 carry a persistent PolyPDF identity and can be selected and deleted. Earlier unmarked strokes and original third-party page paths cannot be distinguished safely and are not selectable by this tool. Other editors may discard insertion marks. Annotation deletion is not a redaction tool.

## Practical limits

Existing text editing is intentionally limited to top-level, unskewed and unrotated text objects. A PDF may split one word into several objects. Embedded subset fonts may not contain replacement characters; unsupported replacements roll back. Text does not automatically reflow, and longer replacements may overlap nearby content. It is not a full word processor. Scanned text requires OCR and cannot be directly edited as a text object.

Move text supports top-level text objects carrying PolyPDF insertion marks. It does not move original third-party text, scans, images, comments, highlights or drawing strokes. Other PDF editors may flatten or discard these marks, preventing later selection. Use Text properties to edit supported block content, font, size (6–160 pt) and custom colour (#RRGGBB or #AARRGGBB) with Apply/Cancel. Blank lines, rotation and drawing order are preserved. Mixed formatting remains read-only.

OCR requires the matching Windows OCR language pack, such as `en-US` or `hr-HR`. Install it in Windows Settings > Time & language > Language & region > Language options. Availability depends on the Windows installation and language. OCR returns text for copying; it does not yet create a searchable PDF text layer. Recognition can contain errors.

PDF forms are displayed but filling interactive fields is not supported yet. Supported standalone text-note comments can be edited and deleted, and ordinary highlights can be removed. Linked/reply/popup, locked, hidden and other unsupported annotation types remain read-only. Annotation changes require the document modification and annotation permissions. Copy restrictions hide note content and disable content editing. Images can be viewed but not edited. The print command prints all pages, scaled to the selected printer's printable area. No cryptographic signing or redaction tools are included.

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

## Software terms and notices

See [LICENSE](LICENSE) for the current software use terms. Releases 0.1.0 and 0.1.1 and their MIT-licensed code retain their existing permissions; the notice is preserved in [LICENSE-MIT-LEGACY.txt](LICENSE-MIT-LEGACY.txt). PDFium, the .NET runtime and other bundled dependencies retain their own licenses. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and the bundled licenses folder.

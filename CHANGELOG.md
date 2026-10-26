# PolyPDF changelog

## 0.2.1

- Replace text-heavy PDF toolbars with compact vector icons grouped for View, Annotate and Pages work.
- Show the active selection, text, move, edit, highlight, drawing and comment tools clearly in light and dark themes.
- Add accessible names, shortcut-aware tooltips, keyboard focus and a More menu for tools that do not fit the available width.

## 0.2.0

- Open and switch PDFs in Fit Width by default. Fit Height remains available manually.
- Delete selected PolyPDF-added text blocks with Delete text or the Delete key while the page has focus.
- Undo and redo an entire multiline deletion in one step, preserving neighbouring content.
- Fixed applying content and formatting changes from Text properties.
- Keep text-field editing separate from PDF deletion and clear selection on empty clicks or page changes.
- Distinguish Edit original text from the properties editor for PolyPDF-added blocks.
- Improve the complete select, move, edit, format and delete workflow, with checks for rotated pages, saving, recovery and updates.

## 0.1.5

- Change the font, point size and colour of supported PolyPDF-added text in Text properties.
- Show the selected block's actual formatting, with support for custom RGB and translucent colours.
- Apply text and formatting together as one undoable edit, preserving position and rotation.
- Preserve repeated spaces and blank lines in edited blocks through saving and reopening.
- Validate unsupported characters, invalid sizes and colours before applying changes.

## 0.1.4

- Inspect the actual text, font, size and colour of selected PolyPDF-added text.
- Edit supported multiline blocks with Apply and Cancel, including blank lines.
- Preserve text position, page rotation and drawing order; undo or redo the whole edit in one step.
- Clearly identify unsupported formatting and reject characters unavailable in the selected font.

## 0.1.3

- Added optional PolyHUB integration for coordinated updates while preserving application save prompts.
- Coordinated setup, uninstall and update locking.

## 0.1.2

- Updated About and installer information to reflect the current software terms and private development model.
- Kept all existing tools available without payment, activation or a subscription.
- Preserved legacy MIT and third-party license notices.

## 0.1.1

- Updated the PolyPDF icon in the application, window title bars, Windows shortcuts and installer.
- Added icon sizes for clear display at different Windows scaling settings.

## 0.1.0

- Initial release of the Windows PDF workspace with an English interface, light and dark themes, and visible version information.
- Open multiple PDFs with tabs, thumbnails, recent files, text selection, search and copying.
- Open documents in Fit Height and adjust the view with Fit Width, Fit Height and manual zoom.
- Add multiline text and edit supported existing PDF text objects.
- Select and move supported PolyPDF-added text, with selection frames, grouped multiline insertions and Undo/Redo.
- Highlight text, draw freehand, add comments and read page comments.
- Rotate, reorder, insert, delete and extract pages, and merge PDF documents.
- Save PDFs, export pages to PNG and print documents.
- Recognise page text locally using Windows OCR and installed language packs.
- Restore unsaved work from encrypted local recovery checkpoints after interrupted sessions.
- Install with optional desktop and Start menu shortcuts and Open with integration.
- Check for updates from GitHub, verify downloads and apply eligible file-level differential updates with full-package fallback.
- View update progress, retry downloads, manage update storage and read release notes inside the application.

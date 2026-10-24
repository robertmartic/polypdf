# PolyPDF roadmap to 1.0

Baseline: 0.1.0, 24 October 2026. The current feature set is retained as the first official release in the new version sequence. Future work below is planned, not implemented. Milestones are readiness gates rather than deadlines or percentages complete.

Before implementation, describe the concrete scope and obtain the user's approval. After approved work and verification, publish matching source and release packages to GitHub. Release descriptions contain user-facing changes and fixes only.

## Version policy

- 0.1.0 is the consolidated baseline, including the installer and updater.
- Use 0.1.1, 0.1.2 and further patch releases for fixes, interface polish and small compatible improvements. A single small feature does not automatically require a new minor version.
- Use 0.2.0 and later minor versions for agreed, coherent groups of new capabilities. Assign their exact version when scope is agreed; the milestones below do not consume a version by themselves.
- Documentation-only changes do not require a new application release.
- Reach 1.0.0 when its acceptance criteria pass. There is no required number of intermediate versions; 0.10.0 is valid if needed.

## Available in the 0.1.0 baseline

- English interface, light/dark themes, version display, active tool-group indicators and embedded Changelog.
- Multiple PDFs, thumbnails, text selection/copy, search, default Fit Height, Fit Width and zoom.
- Added text, limited existing-text editing, selection/movement of supported PolyPDF-added text and grouped multiline insertions.
- Highlights, drawing, comments and Undo/Redo.
- Page rotation/reordering/insertion/deletion/extraction, PDF merging, PNG export and printing.
- Local Windows OCR and encrypted recovery checkpoints for unsaved work.
- Per-user Windows installer, optional shortcuts, Open with registration and user-data-preserving uninstall.
- GitHub update checks, verified full/file-level differential downloads, rollback on handled failures, retry, storage management and post-update Changelog.
- Automated Windows builds, native PDF and updater regression tests, UI checks and package verification.

Text editing is limited by PDF object structure and embedded fonts. Move text supports marked PolyPDF insertions, not arbitrary original content. OCR currently returns copyable text rather than a searchable PDF layer. Form filling, annotation removal, advanced printing and image editing are not yet available. See README.md for the full limits.

## First gate: establish the new baseline

1. Verify the full installer, portable app, source archive, update manifest and maintenance installer all identify 0.1.0.
2. Complete the one-time transition from the retired development versions using the documented normal uninstall/reinstall procedure. Keep downgrade safeguards and preserve user data.
3. Open daily documents, add/move text, exercise Undo/Redo and verify save/reopen in another reader.
4. Prioritise reproducible defects through 0.1.x maintenance releases.
5. On the next approved release, test an actual installed 0.1.0-to-newer update on the user's PC, including shortcut launch, preserved preferences and one-time Changelog.

Exit criterion: the baseline works on normal documents and the first real update in the new sequence is confirmed. Automated updater checks do not replace this installed-app test.

## Text editing and selection

- Add contextual editing of selected added text: supported content, font, size and colour.
- Make selection and supported resize behaviour predictable at different zoom levels and page rotations.
- Retain grouping, PDF insertion marks, Undo/Redo and positions after save/reopen.
- Explain unsupported original text/fonts without damaging existing content; avoid implying Word-style reflow.

Exit criterion: supported text changes survive save/reopen and Undo/Redo, with clear feedback for unsupported cases.

## Comments and annotations

- Select, edit and remove supported comments, highlights and drawing strokes.
- Make the selected item and available actions visible without overcrowding the toolbar.
- Maintain useful comment navigation and predictable cancellation/Undo/Redo.
- Check annotations created by other readers and explicitly identify unsupported types.

Exit criterion: supported annotation changes remain correct in another PDF reader.

## Pages, navigation and printing

- Refine page-range selection, reorder/insert/extract/merge feedback and confirmation for destructive page actions.
- Improve search navigation and result visibility across documents.
- Add printing ranges, copies and useful scaling choices.
- Test rotated pages, mixed page sizes, keyboard navigation and the preservation of supported document structures.

Exit criterion: page operations preserve intended content and printing produces the selected pages; physical printer output needs real hardware verification.

## OCR and scanned documents

- Improve installed-language selection and explain missing Windows OCR language packs.
- Add practical multi-page OCR progress and cancellation where supported.
- Investigate a searchable text layer with alignment and save/reopen checks before committing to its scope.
- Explain recognition errors and retain original scanned content.

Exit criterion: supported scans can be processed predictably and the resulting text has documented limitations.

## Forms and document structure

- Investigate and implement a clearly bounded subset of interactive form filling.
- Preserve supported values after save/reopen and warn before unsupported changes.
- Add useful bookmarks/document information and review their behaviour during page organisation.

Exit criterion: supported forms and navigation structures survive the documented workflows.

## Performance and data integrity

- Maintain a non-private regression corpus covering Unicode/Croatian text, scans, rotations, annotations, forms and protected PDFs.
- Measure long-document responsiveness, rendering/search latency and memory use.
- Improve thumbnail scheduling/cache behaviour and operation progress/cancellation where practical.
- Exercise failed saves, read-only/full destinations, recovery interruptions and damaged recovery copies.
- Review font embedding, signatures/protection warnings and installation/update failure recovery.

Exit criterion: no known reproducible document-loss defect remains in supported everyday workflows; practical limits are documented.

## Accessibility and interface consistency

- Check keyboard-only operation, focus order, visible focus and accessible control names.
- Test light/dark themes, app dialogs, disabled states, display scaling, small windows and multiple displays.
- Keep English terminology, errors and loading states clear and consistent.

Exit criterion: everyday supported operations remain usable and readable across the tested settings.

## Public beta and release candidates

- Use the installed app on normal documents over multiple sessions and collect reproducible, versioned reports without uploading private PDFs automatically.
- Prioritise crashes, document integrity, broken workflows and recurring interface defects.
- Freeze new features for release candidates; verify source/package correspondence, dependency notices and user instructions.
- Test clean installation, upgrades, restart, shortcuts, preferences, recovery and the post-update Changelog.
- Test the supported Windows/platform matrix where hardware or VMs are available and limit support claims to evidence.

Exit criterion: agreed daily workflows pass without release-blocking defects. Further candidates are allowed whenever verification finds failures.

## 1.0.0 acceptance criteria

1. Download an official release, install and launch from the selected shortcuts.
2. Open, read, search and copy supported document text.
3. Add/correct supported text and annotations; save and verify in another reader.
4. Organise pages and print the intended selection.
5. Process the agreed OCR and form workflows with clear limits.
6. Recover the last completed checkpoint after an interrupted session.
7. Upgrade the installed application, preserve preferences and display the correct Changelog once.
8. Find source, licenses, instructions, troubleshooting and known limitations.

Unresolved document-loss defects, misleading update success or broken supported workflows block 1.0. Advanced signing, reliable redaction, conversion, collaboration and full paid-tool parity remain separate proposals.

## Future open-source tools

Agree a shared product identity, visible versions, installation/update behaviour and release notes when the next tool is planned. Reuse proven components while keeping each application's responsibilities and readiness criteria clear.

# Contributing to PolyPDF

PolyPDF is an English-language Windows PDF application written in C# / WPF. The public repository is https://github.com/robertmartic/polypdf. See README.md for the build requirements and ROADMAP.md for planned scope.

## Development

1. Run `tools/bootstrap.ps1` to download the pinned PDFium dependency and check its hashes.
2. Build with `dotnet build PolyPDF.csproj -c Release`.
3. Run `PolyPDF.exe --self-test` from the build output and check its exit code, `test-results/result.txt` and `regressions.txt`.
4. For UI changes, also run `--ui-test` after the self-test and visually inspect the relevant generated screenshots. This requires a Windows desktop session and the English Windows OCR pack.
5. Use `tools/publish.ps1` to prepare a self-contained binary distribution and `tools/package-source.ps1` to prepare a source archive.

Report which checks ran and any checks that could not run. Optional external PDF fixtures require Python libraries described in README.md. They are not included in source archives.

## Changes and bug reports

Keep changes focused. Include the PolyPDF version, Windows version, steps to reproduce and expected/actual behaviour in a bug report. Use a minimal synthetic PDF if a sample is needed; do not publish private documents, passwords or recovery copies. Explain whether a problem affects a scanned PDF or selectable text.

Preserve original documents, keep native PDFium work on PdfWorker, and keep UI changes usable in both themes. Existing text editing is limited by PDF fonts and structure; document unsupported cases. Add meaningful regression coverage when fixing a confirmed defect.

Update CHANGELOG.md for application releases and keep the project version consistent with the distributed executable. Release notes are embedded during compilation. Keep MIT and third-party license notices in source and binary packages. Do not commit generated outputs, downloaded native binaries, local settings or credentials.

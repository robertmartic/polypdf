# GitHub builds and publication

The public project is [robertmartic/polypdf](https://github.com/robertmartic/polypdf). Source uses the MIT license; dependencies retain their notices. Application packages are release assets, not source files.

## Automated Windows checks

`.github/workflows/windows.yml` runs on pushes to main and pull requests. It uses a Windows runner, the SDK selected by `global.json`, and fixed revisions of official GitHub Actions. Build jobs have read-only repository access and do not publish releases.

`tools/ci.ps1` bootstraps checksum-pinned PDFium, restores/builds, runs PDF and updater regression tests and offscreen UI tests, then prepares setup, portable, source, update manifest, maintenance and matching differential packages. It verifies versions, payload hashes and package contents before saving artifacts. Failed checks stop the job. GitHub-hosted UI checks explicitly skip OCR recognition because OCR language packs are not guaranteed there; OCR is tested separately on a Windows desktop with the language pack installed.

The workflow retains verified packages and text reports for 14 days. Personal documents, settings, recovery files, credentials and generated screenshots are excluded from source and releases. The build and package report records the source revision and delta base. This is repeatable preparation, not a claim that timestamped installers are byte-identical on every machine.

Local equivalent, from PowerShell at the repository root:

```powershell
./tools/ci.ps1 -BaseVersion ''
```

Use `-SkipOcr` only when deliberately testing without a Windows OCR language pack. Use `-BaseVersion ''` for a full-only update release. The default base is empty for the new 0.1.0 full baseline. For later releases, explicitly select a lower published version from the new sequence (for example 0.1.0) to produce its delta; other installed versions use the full-package fallback. Never use a retired development version as the base.

## Publishing the tested revision

The owner has authorised publication after approved implementation work passes its checks. New feature scopes still require approval before implementation. Documentation-only changes can be committed without issuing an application release.

For a new version, update the project, English user-facing Changelog and instructions together. Run the Windows workflow manually on main, select the published delta base and enable **Publish a new release after all checks pass**. The publish job runs only after a successful build and only for this repository's main branch. It receives a temporary GitHub token with contents write access; no maintainer credential is embedded in PolyPDF.

`tools/release.ps1` checks the build revision, unchanged branch head, expected asset set and hashes; creates or resumes a draft; uploads all assets; verifies GitHub's returned digests; and only then publishes. It verifies the release tag and anonymously downloads/hash-checks all public assets afterward. It refuses to overwrite an already public version. A failed build cannot enter the publish job. A post-publication download-check failure is reported as a workflow failure and requires investigation; it does not silently rewrite the public release.

The normal release description is generated from that version's Changelog section and contains features and bug fixes only. Keep test instructions, developer coordination and implementation reports out of the update notice.

Keep tags pointing to the tested revision. For manual publication, follow the same package, source and public-download checks. CI build artifacts are downloadable from the Actions run; official app downloads remain under GitHub Releases.

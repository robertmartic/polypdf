param([switch]$NoRestore,[string]$BaseManifest)
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repository
try {
  $publishArguments = @('publish', 'PolyPDF.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', 'dist/PolyPDF', '-p:PublishReadyToRun=false')
  if ($NoRestore) { $publishArguments += '--no-restore' }
  dotnet @publishArguments
  if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
  Copy-Item -LiteralPath README.md, CHANGELOG.md, ROADMAP.md, CONTRIBUTING.md -Destination dist/PolyPDF -Force
  # Retire the legacy copy-only installer from earlier portable distributions.
  foreach ($legacy in @('Install.ps1','Install.cmd')) {
    $legacyPath = Join-Path $repository ('dist/PolyPDF/' + $legacy)
    if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath -Force }
  }
  New-Item -ItemType Directory -Force -Path dist/PolyPDF/docs | Out-Null
  Copy-Item -LiteralPath docs/GITHUB-PUBLISHING.md, docs/INSTALLATION.md, docs/UPDATES.md -Destination dist/PolyPDF/docs -Force
  $testResults = [System.IO.Path]::GetFullPath((Join-Path $repository 'dist\PolyPDF\test-results'))
  $expected = Join-Path $repository 'dist\PolyPDF\test-results'
  if ($testResults -ne $expected) { throw 'Unexpected test output directory.' }
  if (Test-Path -LiteralPath $testResults) { Remove-Item -LiteralPath $testResults -Recurse -Force }
  [xml]$project = Get-Content -LiteralPath PolyPDF.csproj
  $version = [string]$project.Project.PropertyGroup.Version
  & (Join-Path $PSScriptRoot 'build-update-packages.ps1') -BaseManifest $BaseManifest
  Compress-Archive -Path dist/PolyPDF -DestinationPath "dist/PolyPDF-$version-win-x64.zip" -Force
} finally { Pop-Location }

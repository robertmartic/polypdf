param([switch]$SkipPublish,[switch]$NoRestore,[switch]$TestIdentity,[switch]$MaintenanceOnly)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$SkipPublish) { & (Join-Path $PSScriptRoot 'publish.ps1') -NoRestore:$NoRestore }
$compiler = Join-Path $repository 'tmp/installer-tools/nsis-3.13/makensis.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = & (Join-Path $PSScriptRoot 'bootstrap-installer.ps1') }
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'PolyPDF.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$distribution = Join-Path $repository 'dist/PolyPDF'
$publishedVersion = (Get-Item -LiteralPath (Join-Path $distribution 'PolyPDF.exe')).VersionInfo.ProductVersion
# SDK builds inside Git append +commit to informational versions. The numeric
# release must still match; provenance is retained rather than mistaken for drift.
if (!$publishedVersion -or $publishedVersion.Split('+')[0] -ne $version) { throw 'Publish the current version before building setup.' }
$build = Join-Path $repository ('tmp/installer-build/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $build | Out-Null
$files = @(Get-ChildItem -LiteralPath $distribution -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/]test-results[\\/]' })
# Generate explicit owned-file removal instructions. Never recursively delete the app directory.
$commands = @($files | ForEach-Object {
 $relative = $_.FullName.Substring($distribution.Length + 1)
 if ($relative -match '[\$"\r\n]') { throw 'Unsupported filename in installer payload.' }
 'Delete "$INSTDIR\' + $relative + '"'
})
$directories = @($files | ForEach-Object { Split-Path $_.FullName } | Sort-Object -Unique | Where-Object { $_ -ne $distribution } | Sort-Object Length -Descending)
$commands += @($directories | ForEach-Object { 'RMDir "$INSTDIR\' + $_.Substring($distribution.Length + 1) + '"' })
$manifest = Join-Path $build 'owned-files.nsh'
$commands | Set-Content -LiteralPath $manifest -Encoding UTF8
$suffix = if ($TestIdentity) { '-test' } else { '' }
$kind = if ($MaintenanceOnly) { 'maintenance' } else { 'setup' }
$output = Join-Path $repository "dist/PolyPDF-$version-$kind$suffix.exe"
$arguments = @('/V2',"/DAPP_VERSION=$version","/DPAYLOAD=$distribution","/DOUTPUT=$output","/DOWNED_FILES=$manifest","/DPAYLOAD_KB=$([math]::Ceiling(($files | Measure-Object Length -Sum).Sum / 1KB))")
if ($TestIdentity) { $arguments += '/DINSTALLER_TEST' }
if ($MaintenanceOnly) { $arguments += '/DMAINTENANCE_ONLY' }
$arguments += (Join-Path $repository 'installer/PolyPDF.nsi')
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed.' }
Get-Item -LiteralPath $output | Select-Object FullName,Length

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$vendor = Join-Path $repository 'vendor'
New-Item -ItemType Directory -Path $vendor -Force | Out-Null
$version = '151.0.7920'
$package = Join-Path $vendor 'pdfium.zip'
Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/bblanchon.pdfium.win32/$version/bblanchon.pdfium.win32.$version.nupkg" -OutFile $package
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne '0C80CAB2DF3EC3B399447FB155984002FAB327263952090587057CDC259D9E6A') {
  throw 'PDFium NuGet checksum mismatch. Do not use this download.'
}
Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $vendor 'pdfium-package') -Force
$archive = Join-Path $vendor 'pdfium-release.tgz'
Invoke-WebRequest -Uri 'https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F7920/pdfium-win-x64.tgz' -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne 'BF25149815B34B00042F48A886653D469C817529DD9CCCABB4B509B6465A9526') {
  throw 'PDFium release checksum mismatch. Do not use this download.'
}
$licenses = Join-Path $vendor 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
tar -xf $archive -C $licenses LICENSE licenses
if ($LASTEXITCODE -ne 0) { throw 'Could not extract PDFium licenses.' }
Write-Host 'PDFium and its licenses are ready.'

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolRoot = Join-Path $repository 'tmp/installer-tools'
$archive = Join-Path $toolRoot 'nsis-3.13-verified.zip'
$expectedHash = 'BA63DFFC4410EE89193E1CB5A41989991BD77C61068DA17E3156D136B7B0B3D8'
New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
 Invoke-WebRequest -Uri 'https://downloads.sourceforge.net/project/nsis/NSIS%203/3.13/nsis-3.13.zip' -OutFile $archive
 # SourceForge sometimes returns an HTML download page instead of the archive.
 $bytes = [IO.File]::ReadAllBytes($archive)
 if ($bytes.Length -lt 2 -or $bytes[0] -ne 80 -or $bytes[1] -ne 75) {
  $html = [Text.Encoding]::UTF8.GetString($bytes)
  $redirect = [regex]::Match($html,'<meta http-equiv="refresh" content="5; url=([^"]+)"')
  if (!$redirect.Success) { throw 'SourceForge did not return a ZIP archive or download redirect.' }
  $uri = [Net.WebUtility]::HtmlDecode($redirect.Groups[1].Value)
  if (!$uri.StartsWith('https://downloads.sourceforge.net/project/nsis/')) { throw 'Unexpected download destination.' }
  Invoke-WebRequest -Uri $uri -OutFile $archive
 }
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'NSIS SHA256 mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot -Force
Write-Output (Join-Path $toolRoot 'nsis-3.13/makensis.exe')

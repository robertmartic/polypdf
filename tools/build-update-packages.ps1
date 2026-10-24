param([string]$BaseManifest)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distribution = Join-Path $repository 'dist/PolyPDF'
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'PolyPDF.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$base = if ($BaseManifest) { Get-Content -LiteralPath $BaseManifest -Raw | ConvertFrom-Json } else { $null }
$files = @(Get-ChildItem -LiteralPath $distribution -Recurse -File | Where-Object { $_.Name -ne 'update-manifest.json' -and $_.FullName -notmatch '[\\/]test-results[\\/]' } | ForEach-Object {
 [ordered]@{Path=$_.FullName.Substring($distribution.Length+1).Replace('\','/');Size=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest = [ordered]@{Format=1;Version=$version;Files=$files;DeltaFrom=if ($base) { $base.Version } else { $null }}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
$manifestPath = Join-Path $repository "dist/PolyPDF-$version-manifest.json"
[IO.File]::WriteAllText($manifestPath,$manifestJson)
[IO.File]::WriteAllText((Join-Path $distribution 'update-manifest.json'),$manifestJson)
if ($base) {
 if ([version]$base.Version -ge [version]$version -or $base.Format -ne 1) { throw 'Invalid base manifest.' }
 $changes = @($files | Where-Object {
  $file = $_; $old = $base.Files | Where-Object { $_.Path -eq $file.Path } | Select-Object -First 1
  !$old -or $old.Sha256 -ne $file.Sha256
 })
 $archivePath = Join-Path $repository "dist/PolyPDF-$($base.Version)-to-$version-delta.zip"
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $temporary = Join-Path $repository ('tmp/delta-' + [guid]::NewGuid().ToString('N') + '.zip')
 $zip = [IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
 try {
  foreach ($file in $changes) {
   [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $distribution $file.Path),('PolyPDF/' + $file.Path),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
 } finally { $zip.Dispose() }
 Move-Item -LiteralPath $temporary -Destination $archivePath -Force
 [pscustomobject]@{BaseVersion=$base.Version;Version=$version;ChangedFiles=$changes.Count;Bytes=(Get-Item -LiteralPath $archivePath).Length;Path=$archivePath}
}

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'PolyPDF.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$stageParent = Join-Path $repository ("tmp\source-$version-" + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $stageParent 'PolyPDF'
New-Item -ItemType Directory -Force -Path $stage,(Join-Path $stage 'vendor') | Out-Null
$names = @('AGENTS.md','.gitignore','.gitattributes','global.json','PolyPDF.csproj','app.manifest','LICENSE','README.md','CHANGELOG.md','ROADMAP.md','CONTRIBUTING.md','THIRD-PARTY-NOTICES.txt')
Get-ChildItem -LiteralPath $repository -File | Where-Object { $_.Extension -in @('.cs','.xaml') -or $_.Name -in $names } | Copy-Item -Destination $stage -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tools'),(Join-Path $stage 'assets'),(Join-Path $stage 'docs') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repository 'tools') -File | Where-Object { $_.Extension -in @('.ps1','.py') } | Copy-Item -Destination (Join-Path $stage 'tools')
Copy-Item -LiteralPath (Join-Path $repository 'assets\polypdf.ico'),(Join-Path $repository 'assets\polypdf.png') -Destination (Join-Path $stage 'assets')
Copy-Item -LiteralPath (Join-Path $repository 'docs\GITHUB-PUBLISHING.md'),(Join-Path $repository 'docs\INSTALLATION.md'),(Join-Path $repository 'docs\UPDATES.md') -Destination (Join-Path $stage 'docs')
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'installer') | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'installer\PolyPDF.nsi') -Destination (Join-Path $stage 'installer')
Copy-Item -LiteralPath (Join-Path $repository '.github') -Destination (Join-Path $stage '.github') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repository 'vendor\licenses') -Destination (Join-Path $stage 'vendor') -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $repository 'dist') | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path $repository "dist\PolyPDF-$version-source.zip"
$temporaryArchive = Join-Path $stageParent 'source.zip'
if ($env:GITHUB_SHA -and (Test-Path -LiteralPath (Join-Path $repository '.git'))) {
 $head = git -C $repository rev-parse HEAD
 if ($LASTEXITCODE -ne 0 -or $head -ne $env:GITHUB_SHA) { throw 'Source archive revision mismatch.' }
 # Export original Git blobs, including binary notices and original line endings.
 git -C $repository archive --format=zip --prefix=PolyPDF/ "--output=$temporaryArchive" $head
 if ($LASTEXITCODE -ne 0) { throw 'Git source export failed.' }
 [System.IO.Compression.ZipFile]::ExtractToDirectory($temporaryArchive,$stageParent,$true)
} else {
 [System.IO.Compression.ZipFile]::CreateFromDirectory($stage,$temporaryArchive,[System.IO.Compression.CompressionLevel]::Optimal,$true)
}
Move-Item -LiteralPath $temporaryArchive -Destination $archive -Force
[pscustomobject]@{ SourcePath = $stage; ArchivePath = $archive; FileCount = @(Get-ChildItem -LiteralPath $stage -Recurse -File).Count }

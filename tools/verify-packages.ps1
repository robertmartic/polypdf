param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist=Join-Path $root 'dist'
$manifestPath=Join-Path $dist "PolyPDF-$Version-manifest.json"
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if($manifest.Version -ne $Version -or $manifest.Format -ne 1){throw 'Wrong update manifest version'}
$names=@("PolyPDF-$Version-setup.exe","PolyPDF-$Version-maintenance.exe","PolyPDF-$Version-win-x64.zip","PolyPDF-$Version-source.zip","PolyPDF-$Version-manifest.json")
if($manifest.DeltaFrom){$names += "PolyPDF-$($manifest.DeltaFrom)-to-$Version-delta.zip"}
foreach($file in $manifest.Files){
 if($file.Path -match '(^/|\\|:|(^|/)\.\.(/|$))'){throw 'Unsafe manifest path'}
 $path=Join-Path $dist ('PolyPDF/'+$file.Path)
 if((Get-Item -LiteralPath $path).Length -ne $file.Size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256){throw ('Manifest mismatch: '+$file.Path)}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($name in $names | Where-Object {$_ -like '*.zip'}){
 $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $dist $name))
 try {
  if(@($zip.Entries | Where-Object FullName -match '(?i)(test-results/|\.git/|(^|/)settings.json$|pending-result|\.pdf$|\.pfx$|\.pem$)').Count){throw ('Unexpected private or generated file in '+$name)}
  if($name -like '*-source.zip'){
   foreach($required in @('PolyPDF/PolyPDF.csproj','PolyPDF/global.json','PolyPDF/.github/workflows/windows.yml','PolyPDF/tools/ci.ps1','PolyPDF/LICENSE')){
    if(!$zip.GetEntry($required)){throw ('Source package missing '+$required)}
   }
  }
  if($name -like '*-win-x64.zip'){
   $entry=$zip.GetEntry('PolyPDF/update-manifest.json');if(!$entry){throw 'Portable package has no update manifest'}
   $reader=[IO.StreamReader]::new($entry.Open());try{$embedded=$reader.ReadToEnd()}finally{$reader.Dispose()}
   if($embedded -cne [IO.File]::ReadAllText($manifestPath)){throw 'Portable manifest differs'}
   foreach($file in $manifest.Files){
    $entry=$zip.GetEntry('PolyPDF/'+$file.Path);if(!$entry -or $entry.Length -ne $file.Size){throw 'Portable contents mismatch'}
    $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try{$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose()}
    if($hash -ne $file.Sha256){throw ('Portable hash mismatch '+$file.Path)}
   }
  }
  if($name -like '*-delta.zip'){
   foreach($entry in $zip.Entries){
    $file=$manifest.Files | Where-Object {('PolyPDF/'+$_.Path) -ceq $entry.FullName} | Select-Object -First 1
    if(!$file -or $entry.Length -ne $file.Size){throw 'Delta contains unexpected file'}
    $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try{$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose()}
    if($hash -ne $file.Sha256){throw 'Delta hash mismatch'}
   }
  }
 } finally {$zip.Dispose()}
}
foreach($kind in @('setup','maintenance')){
 if((Get-Item -LiteralPath (Join-Path $dist "PolyPDF-$Version-$kind.exe")).VersionInfo.ProductVersion -ne $Version){throw 'Installer version mismatch'}
}
$checks=@($names | ForEach-Object {$path=Join-Path $dist $_; [pscustomobject]@{name=$_;bytes=(Get-Item -LiteralPath $path).Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}})
[pscustomobject]@{version=$Version;files=$checks} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dist 'package-checks.json')
Write-Output ('PASS: versions, all manifest hashes, portable/delta contents, source and package privacy checks for '+$Version)

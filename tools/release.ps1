param([Parameter(Mandatory=$true)][string]$Commit)
$ErrorActionPreference='Stop'
if($Commit -notmatch '^[a-f0-9]{40}$' -or !$env:GH_TOKEN){throw 'Missing release identity or GitHub token'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist=Join-Path $root 'dist'
$report=Get-Content -LiteralPath (Join-Path $dist 'build-report.json') -Raw | ConvertFrom-Json
$checks=Get-Content -LiteralPath (Join-Path $dist 'package-checks.json') -Raw | ConvertFrom-Json
[xml]$project=Get-Content -LiteralPath (Join-Path $root 'PolyPDF.csproj');$version=[string]$project.Project.PropertyGroup.Version
if($report.revision -ne $Commit -or $report.version -ne $version -or $checks.version -ne $version){throw 'Build provenance mismatch'}
$headers=@{Authorization='Bearer '+$env:GH_TOKEN;Accept='application/vnd.github+json';'User-Agent'='PolyPDF-release';'X-GitHub-Api-Version'='2022-11-28'}
$public=@{Accept='application/vnd.github+json';'User-Agent'='PolyPDF-public-verification'}
$api='https://api.github.com/repos/robertmartic/polypdf'
try {
 if((Invoke-RestMethod -Uri "$api/git/ref/heads/main" -Headers $headers).object.sha -ne $Commit){throw 'Main branch changed; prepare a new build'}
 $existingTags=@(Invoke-RestMethod -Uri "$api/git/matching-refs/tags/v$version" -Headers $headers | ForEach-Object {$_})
 $existingTag=$existingTags | Where-Object ref -eq "refs/tags/v$version" | Select-Object -First 1
 if($existingTag -and ($existingTag.object.type -ne 'commit' -or $existingTag.object.sha -ne $Commit)){throw 'An existing release tag points to another revision'}
 foreach($file in $checks.files){
  if([IO.Path]::GetFileName($file.name) -cne $file.name){throw 'Unsafe asset filename'}
  $path=Join-Path $dist $file.name
  if((Get-Item -LiteralPath $path).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256){throw 'Prepared package hash mismatch'}
 }
 $manifest=Get-Content -LiteralPath (Join-Path $dist "PolyPDF-$version-manifest.json") -Raw | ConvertFrom-Json
 $expected=@("PolyPDF-$version-setup.exe","PolyPDF-$version-maintenance.exe","PolyPDF-$version-win-x64.zip","PolyPDF-$version-source.zip","PolyPDF-$version-manifest.json")
 if($manifest.DeltaFrom){$expected += "PolyPDF-$($manifest.DeltaFrom)-to-$version-delta.zip"}
 if(@(Compare-Object ($expected | Sort-Object) ($checks.files.name | Sort-Object)).Count){throw 'Unexpected release asset set'}
 $history=[IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
 $section=[regex]::Match($history,'(?ms)^## '+[regex]::Escape($version)+'\s*\r?\n(.*?)(?=^## |\z)')
 if(!$section.Success -or !$section.Groups[1].Value.Trim()){throw 'Missing user-facing release notes'}
 $notes=$section.Groups[1].Value.Trim()
 $releases=@(Invoke-RestMethod -Uri "$api/releases?per_page=100" -Headers $headers | ForEach-Object {$_})
 $release=$releases | Where-Object tag_name -eq "v$version" | Select-Object -First 1
 if($release -and !$release.draft){throw 'This version is already public; never overwrite a published release'}
 if(!$release){
  $body=@{tag_name="v$version";target_commitish=$Commit;name="PolyPDF $version";body=$notes;draft=$true;prerelease=$false} | ConvertTo-Json
  $release=Invoke-RestMethod -Uri "$api/releases" -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
 }
 foreach($file in $checks.files){
  $assets=@(Invoke-RestMethod -Uri "$api/releases/$($release.id)/assets" -Headers $headers | ForEach-Object {$_})
  $asset=$assets | Where-Object name -eq $file.name | Select-Object -First 1
  if(!$asset){
   $url=$release.upload_url.Split('{')[0]+'?name='+[Uri]::EscapeDataString($file.name)
   $asset=Invoke-RestMethod -Uri $url -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile (Join-Path $dist $file.name)
  }
  if($asset.size -ne $file.bytes -or $asset.digest -ne ('sha256:'+$file.sha256) -or $asset.state -ne 'uploaded'){throw 'GitHub asset verification failed'}
 }
 $assets=@(Invoke-RestMethod -Uri "$api/releases/$($release.id)/assets" -Headers $headers | ForEach-Object {$_})
 if($assets.Count -ne $expected.Count){throw 'Unexpected draft assets'}
 $payload=@{draft=$false;make_latest='true';body=$notes} | ConvertTo-Json
 $release=Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Method Patch -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
 # Read canonical public URLs after publication; draft URLs may be temporary.
 $release=Invoke-RestMethod -Uri "$api/releases/tags/v$version" -Headers $public
 $tag=Invoke-RestMethod -Uri "$api/git/ref/tags/v$version" -Headers $public
 if($tag.object.sha -ne $Commit){throw 'Published tag revision mismatch'}
 $downloads=Join-Path $root ('tmp/public-release-'+$version);New-Item -ItemType Directory -Force $downloads | Out-Null
 foreach($asset in $release.assets){
  $file=$checks.files | Where-Object name -eq $asset.name | Select-Object -First 1
  $path=Join-Path $downloads $asset.name
  Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $path
  if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256){throw 'Anonymous public download hash mismatch'}
  Write-Output ('PASS: public asset '+$asset.name)
 }
 Write-Output ('Published and verified '+$release.html_url)
} finally {$headers.Clear()}

param([string]$BaseVersion='',[switch]$SkipOcr)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
 if($BaseVersion -and $BaseVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid delta base version'}
 if(!(Test-Path -LiteralPath 'vendor/pdfium-package/runtimes/win-x64/native/pdfium.dll')){& ./tools/bootstrap.ps1}
 dotnet restore PolyPDF.csproj -r win-x64 -p:SelfContained=true
 if($LASTEXITCODE -ne 0){throw 'Restore failed'}
 dotnet build PolyPDF.csproj -c Release -r win-x64 --self-contained true --no-restore --nologo
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
 $exe=Join-Path $root 'bin/Release/net10.0-windows10.0.19041.0/win-x64/PolyPDF.exe'
 $results=Join-Path (Split-Path $exe) 'test-results'
 foreach($mode in @('--self-test','--ui-test')){
  $arguments=@($mode); if($mode -eq '--ui-test' -and $SkipOcr){$arguments += '--skip-ocr'}
  $p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
  if(!$p.WaitForExit(180000)){$p.Kill();throw ('Test timed out: '+$mode)}
  if($p.ExitCode -ne 0){Get-ChildItem -LiteralPath $results -Filter '*.txt' | ForEach-Object {Get-Content -LiteralPath $_.FullName};throw ('Tests failed: '+$mode)}
 }
 foreach($name in @('result.txt','regressions.txt','update-tests.txt','update-storage-tests.txt','update-ui-tests.txt','ui-result.txt','default-fit-tests.txt','text-move-tests.txt','text-move-ui-tests.txt')){
  if(!(Test-Path -LiteralPath (Join-Path $results $name)) -or (Get-Content -LiteralPath (Join-Path $results $name) -Raw) -notmatch '^PASS'){throw ('Missing passing test report '+$name)}
 }
 New-Item -ItemType Directory -Force output/ci | Out-Null
 Get-ChildItem -LiteralPath $results -Filter '*.txt' | Copy-Item -Destination output/ci -Force
 $baseManifest=$null
 if($BaseVersion){
  $headers=@{'User-Agent'='PolyPDF-ci';Accept='application/vnd.github+json'}
  $release=Invoke-RestMethod -Uri "https://api.github.com/repos/robertmartic/polypdf/releases/tags/v$BaseVersion" -Headers $headers
  $name="PolyPDF-$BaseVersion-manifest.json";$asset=$release.assets | Where-Object name -eq $name | Select-Object -First 1
  if(!$asset -or $asset.size -gt 4MB -or $asset.digest -notmatch '^sha256:[a-f0-9]{64}$' -or $asset.browser_download_url -cne "https://github.com/robertmartic/polypdf/releases/download/v$BaseVersion/$name"){throw 'Invalid published base manifest asset'}
  New-Item -ItemType Directory -Force tmp/ci | Out-Null
  $baseManifest=Join-Path $root ('tmp/ci/'+$name)
  Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $baseManifest
  if(('sha256:'+(Get-FileHash -LiteralPath $baseManifest -Algorithm SHA256).Hash.ToLowerInvariant()) -ne $asset.digest){throw 'Base manifest hash mismatch'}
 }
 & ./tools/publish.ps1 -NoRestore -BaseManifest $baseManifest
 & ./tools/build-installer.ps1 -SkipPublish
 & ./tools/build-installer.ps1 -SkipPublish -MaintenanceOnly
 & ./tools/package-source.ps1
 [xml]$project=Get-Content PolyPDF.csproj; $version=[string]$project.Project.PropertyGroup.Version
 & ./tools/verify-packages.ps1 -Version $version
 [pscustomobject]@{version=$version;revision=$env:GITHUB_SHA;base=$BaseVersion;sdk=(dotnet --version);runtime=(Get-Item dist/PolyPDF/coreclr.dll).VersionInfo.ProductVersion;ocr=if($SkipOcr){'skipped explicitly'}else{'passed'}} | ConvertTo-Json | Set-Content dist/build-report.json
} finally {
 if($results -and (Test-Path -LiteralPath $results)){New-Item -ItemType Directory -Force output/ci | Out-Null; Get-ChildItem -LiteralPath $results -Filter '*.txt' | Copy-Item -Destination output/ci -Force}
 Pop-Location
}

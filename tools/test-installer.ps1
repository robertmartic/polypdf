$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'PolyPDF.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$setup = Join-Path $repository "dist/PolyPDF-$version-setup-test.exe"
if (!(Test-Path -LiteralPath $setup)) { throw 'Build the -TestIdentity installer first.' }
$registry = 'HKCU:\Software\PolyPDF.InstallerTests'
$uninstallRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PolyPDF.InstallerTests'
if (Test-Path -LiteralPath $registry) { throw 'A previous installer test remains registered. Inspect it before retrying.' }
$testRoot = Join-Path $repository ('tmp/installer-lifecycle-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $testRoot 'PolyPDF test ž'
$report = Join-Path $repository 'output/installer-checks'
New-Item -ItemType Directory -Force -Path $testRoot,$report | Out-Null
function Assert([bool]$condition,[string]$message) { if (!$condition) { throw $message } }
function RunSetup([string]$arguments,[int]$expected=0) {
 $process = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
 Assert ($process.ExitCode -eq $expected) "Unexpected setup exit code $($process.ExitCode); expected $expected."
}
function PreferencesSnapshot {
 $root = Join-Path $env:LOCALAPPDATA 'PolyPDF'
 $entries = @(Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'settings.json' -or $_.Extension -eq '.recovery' } | ForEach-Object { $_.FullName + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
 return ($entries | Sort-Object) -join "`n"
}
$preferencesBefore = PreferencesSnapshot
$defaultPdfBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty ProgId -ErrorAction SilentlyContinue)
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'PolyPDF Installer Tests.lnk'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'PolyPDF Installer Tests.lnk'
Assert (!(Test-Path -LiteralPath $desktop) -and !(Test-Path -LiteralPath $startMenu)) 'Test shortcuts already exist.'

$unrelated = Join-Path $testRoot 'unrelated'
New-Item -ItemType Directory -Force -Path $unrelated | Out-Null
Set-Content -LiteralPath (Join-Path $unrelated 'keep.txt') 'unrelated user file'
RunSetup "/S /D=$unrelated" 2
Assert ((Get-Content -LiteralPath (Join-Path $unrelated 'keep.txt')) -eq 'unrelated user file') 'Unrelated folder was changed.'
Write-Output 'PASS: unrelated nonempty folder rejected.'

RunSetup "/S /DESKTOP=0 /STARTMENU=0 /D=$install"
$exe = Join-Path $install 'PolyPDF.exe'
Assert ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion.Split('+')[0] -eq $version) 'Installed executable has the wrong version.'
Assert ((Get-ItemProperty -LiteralPath $uninstallRegistry).DisplayVersion -eq $version) 'Windows installed-app version is incorrect.'
Assert (!(Test-Path -LiteralPath $desktop) -and !(Test-Path -LiteralPath $startMenu)) 'Deselected shortcuts were created.'
$command = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Classes\Applications\PolyPDF.InstallerTests.exe\shell\open\command').'(default)'
Assert ($command -eq ('"' + $exe + '" "%1"')) 'Open with command is incorrect.'
Write-Output 'PASS: clean install, Unicode/spaced folder, registration, no-shortcut options and Open with.'

$canary = Join-Path $install 'personal-test-document.txt'
Set-Content -LiteralPath $canary 'keep this user-owned file'
Set-ItemProperty -LiteralPath $uninstallRegistry -Name DisplayVersion -Value '0.0.9'
RunSetup '/S /DESKTOP=1 /STARTMENU=1'
Assert ((Get-ItemProperty -LiteralPath $uninstallRegistry).DisplayVersion -eq $version) 'Upgrade did not update registered version.'
Assert ((Get-Content -LiteralPath $canary) -eq 'keep this user-owned file') 'Reinstall changed an unrelated file.'
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcut in @($desktop,$startMenu)) {
 Assert (Test-Path -LiteralPath $shortcut) 'Selected shortcut missing.'
 $link = $shell.CreateShortcut($shortcut)
 Assert ($link.TargetPath -eq $exe -and $link.WorkingDirectory -eq $install) 'Shortcut target or working directory is incorrect.'
}
Write-Output 'PASS: simulated earlier-version registration upgrades in place and both shortcut options work.'

Set-ItemProperty -LiteralPath $uninstallRegistry -Name DisplayVersion -Value '9.0.0'
RunSetup '/S' 2
Assert ((Get-ItemProperty -LiteralPath $uninstallRegistry).DisplayVersion -eq '9.0.0') 'Downgrade guard modified registration.'
Set-ItemProperty -LiteralPath $uninstallRegistry -Name DisplayVersion -Value $version
RunSetup ("/S /D=" + (Join-Path $testRoot 'other location')) 2
$running = [Threading.Mutex]::new($false,'Local\PolyPDF.InstallerTests.Running')
try {
 RunSetup '/S' 2
 # Suppress the normal uninstaller self-copy only for the blocked-exit-code check.
 $blocked = Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList "/S _?=$install" -WindowStyle Hidden -Wait -PassThru
 Assert ($blocked.ExitCode -eq 2) 'Uninstall did not reject the running-app marker.'
 Assert (Test-Path -LiteralPath $exe) 'Busy guard allowed removal of application files.'
} finally { $running.Dispose() }
Write-Output 'PASS: downgrade, relocation and running-app installation/uninstallation guards.'

$uninstaller = Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList '/S' -WindowStyle Hidden -Wait -PassThru
Assert ($uninstaller.ExitCode -eq 0) 'Uninstaller returned an error.'
$deadline = [DateTime]::UtcNow.AddSeconds(15)
while ((Test-Path -LiteralPath $uninstallRegistry) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
Assert (!(Test-Path -LiteralPath $uninstallRegistry) -and !(Test-Path -LiteralPath $registry)) 'Uninstall registration remains.'
Assert (!(Test-Path -LiteralPath $exe)) 'Program executable remains after uninstall.'
Assert (!(Test-Path -LiteralPath $desktop) -and !(Test-Path -LiteralPath $startMenu)) 'Shortcuts remain after uninstall.'
Assert (!(Test-Path -LiteralPath 'HKCU:\Software\Classes\Applications\PolyPDF.InstallerTests.exe')) 'Open with registration remains.'
Assert ((Get-Content -LiteralPath $canary) -eq 'keep this user-owned file') 'Uninstall removed an unrelated file.'
Assert ((PreferencesSnapshot) -ceq $preferencesBefore) 'User preferences or recovery data changed.'
$defaultPdfAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty ProgId -ErrorAction SilentlyContinue)
Assert ($defaultPdfAfter -eq $defaultPdfBefore) 'The default PDF app changed.'
Write-Output 'PASS: uninstall removes owned files/registration/shortcuts and preserves unrelated files, preferences, recovery data and PDF default.'
@('PASS: isolated per-user NSIS lifecycle checks.','Clean install in a Unicode/spaced path; shortcut deselection/selection; Open with registration; simulated upgrade; downgrade/relocation/busy guards; owned-file uninstall; unrelated-file, preference/recovery and default-association preservation.','Earlier-version upgrade uses simulated registry metadata, not a previously released installer binary.','Test installation path: ' + $install) | Set-Content -LiteralPath (Join-Path $report 'lifecycle.txt')

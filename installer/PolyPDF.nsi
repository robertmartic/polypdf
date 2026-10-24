; PolyPDF per-user installer. Built with the unmodified NSIS zlib stub.
Unicode true
RequestExecutionLevel user
ManifestSupportedOS all
!include MUI2.nsh
!include LogicLib.nsh
!include FileFunc.nsh
!include Sections.nsh
!include WordFunc.nsh
!include x64.nsh
!include WinVer.nsh

!ifdef INSTALLER_TEST
 !define APP_ID "PolyPDF.InstallerTests"
 !define APP_NAME "PolyPDF Installer Tests"
 !define APP_REG "Software\PolyPDF.InstallerTests"
 !define APP_FILE_ID "PolyPDF.InstallerTests.exe"
 !define APP_PROG_ID "PolyPDF.InstallerTests.Document"
!else
 !define APP_ID "PolyPDF"
 !define APP_NAME "PolyPDF"
 !define APP_REG "Software\PolyPDF"
 !define APP_FILE_ID "PolyPDF.exe"
 !define APP_PROG_ID "PolyPDF.Document"
!endif
!define UNINSTALL_REG "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
!define OPEN_COMMAND '$\"$INSTDIR\PolyPDF.exe$\" $\"%1$\"'

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\${APP_ID}"
InstallDirRegKey HKCU "${APP_REG}" "InstallDir"
BrandingText "PolyPDF - free and open source"
SetCompressor /SOLID zlib
SetOverwrite on
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "PolyPDF Setup"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "PolyPDF per-user installer"
VIAddVersionKey /LANG=1033 "LegalCopyright" "2026 PolyPDF contributors"
!define MUI_ICON "..\assets\polypdf.ico"
!define MUI_UNICON "..\assets\polypdf.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Install ${APP_NAME} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "A free, open-source PDF workspace for Windows.$\r$\n$\r$\nSetup installs for your Windows account. No administrator access is needed.$\r$\n$\r$\nClose PolyPDF before continuing. Your preferences and recovery copies will be preserved."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateDirectory
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\PolyPDF.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!define MUI_UNCONFIRMPAGE_TEXT_TOP "PolyPDF program files and its shortcuts will be removed. Preferences, recovery copies and your PDF documents will be kept."
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"

Var RegisteredDir
Var SetupMutex
Var Parameters
Var Choice

!macro Reject Message
 FileOpen $8 "$TEMP\${APP_ID}-setup-error.txt" w
 FileWriteUTF16LE /BOM $8 "${Message}$\r$\nFolder: $INSTDIR$\r$\n"
 FileClose $8
 IfSilent +2
 MessageBox MB_OK|MB_ICONEXCLAMATION "${Message}"
 SetErrorLevel 2
 Abort
!macroend

!macro RunningGuard
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\${APP_ID}.Running") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  !insertmacro Reject "Close all PolyPDF windows, then run setup again. No files have been changed."
 ${EndIf}
!macroend

!macro SetupLock
 System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\${APP_ID}.Setup") p.r0 ?e'
 Pop $1
 StrCpy $SetupMutex $0
 ${If} $1 == 183
  !insertmacro Reject "Another PolyPDF setup or uninstall is already running."
 ${EndIf}
!macroend

!macro UpdateGuard
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\PolyPDF.Update") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  !insertmacro Reject "A PolyPDF update is running. Wait for it to finish."
 ${EndIf}
!macroend


Function ValidateDirectory
 ; NSIS GetFullPathName can fail for a directory that does not exist yet.
 ; The Win32 API canonicalises new paths without creating them.
 System::Call 'kernel32::GetFullPathNameW(w "$INSTDIR", i ${NSIS_MAX_STRLEN}, w .r2, p 0) i.r3'
 ${If} $3 == 0
 ${OrIf} $3 >= ${NSIS_MAX_STRLEN}
  !insertmacro Reject "Choose a valid absolute installation path shorter than ${NSIS_MAX_STRLEN} characters."
 ${EndIf}
 StrCpy $INSTDIR $2
 ${GetRoot} "$INSTDIR" $0
 ${If} $INSTDIR == "$0\"
 ${OrIf} $INSTDIR == $0
  !insertmacro Reject "Choose a dedicated PolyPDF folder, not a drive root."
 ${EndIf}
 ${If} $RegisteredDir != ""
  ${If} $INSTDIR != $RegisteredDir
   !insertmacro Reject "PolyPDF is already installed in $RegisteredDir. Use that folder to upgrade. To move it, uninstall first; your preferences will be kept."
  ${EndIf}
 ${EndIf}
 ReadINIStr $0 "$INSTDIR\polypdf-installation.ini" "PolyPDF" "AppId"
 ${If} $0 == "${APP_ID}"
  Return
 ${EndIf}
 ; An existing unowned folder must be empty, even for silent installations.
 FindFirst $0 $1 "$INSTDIR\*"
 loop:
 ${If} $1 == ""
  Goto empty
 ${EndIf}
 ${If} $1 != "."
 ${AndIf} $1 != ".."
  FindClose $0
  !insertmacro Reject "The selected folder contains files that are not managed by this installer. Choose a new, empty folder."
 ${EndIf}
 FindNext $0 $1
 Goto loop
 empty:
 FindClose $0
 ClearErrors
FunctionEnd

Section "PolyPDF application (required)" MainSection
 SectionIn RO
 !insertmacro RunningGuard
 Call ValidateDirectory
 SetOutPath "$INSTDIR"
 !ifndef MAINTENANCE_ONLY
  File /r /x test-results "${PAYLOAD}\*"
 !endif
 WriteINIStr "$INSTDIR\polypdf-installation.ini" "PolyPDF" "AppId" "${APP_ID}"
 WriteINIStr "$INSTDIR\polypdf-installation.ini" "PolyPDF" "Version" "${APP_VERSION}"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "${APP_REG}" "InstallDir" "$INSTDIR"
 WriteRegStr HKCU "${UNINSTALL_REG}" "DisplayName" "${APP_NAME}"
 WriteRegStr HKCU "${UNINSTALL_REG}" "DisplayVersion" "${APP_VERSION}"
 WriteRegStr HKCU "${UNINSTALL_REG}" "Publisher" "PolyPDF contributors"
 WriteRegStr HKCU "${UNINSTALL_REG}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "${UNINSTALL_REG}" "DisplayIcon" "$INSTDIR\PolyPDF.exe,0"
 WriteRegStr HKCU "${UNINSTALL_REG}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKCU "${UNINSTALL_REG}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
 WriteRegStr HKCU "${UNINSTALL_REG}" "URLInfoAbout" "https://github.com/robertmartic/polypdf"
 WriteRegDWORD HKCU "${UNINSTALL_REG}" "EstimatedSize" ${PAYLOAD_KB}
 WriteRegDWORD HKCU "${UNINSTALL_REG}" "NoModify" 1
 WriteRegDWORD HKCU "${UNINSTALL_REG}" "NoRepair" 1
 WriteRegStr HKCU "Software\Classes\Applications\${APP_FILE_ID}" "FriendlyAppName" "${APP_NAME}"
 WriteRegStr HKCU "Software\Classes\Applications\${APP_FILE_ID}\shell\open\command" "" '${OPEN_COMMAND}'
 WriteRegStr HKCU "Software\Classes\Applications\${APP_FILE_ID}\SupportedTypes" ".pdf" ""
 WriteRegStr HKCU "Software\Classes\${APP_PROG_ID}" "" "PDF document"
 WriteRegStr HKCU "Software\Classes\${APP_PROG_ID}\DefaultIcon" "" "$INSTDIR\PolyPDF.exe,0"
 WriteRegStr HKCU "Software\Classes\${APP_PROG_ID}\shell\open\command" "" '${OPEN_COMMAND}'
 WriteRegStr HKCU "Software\Classes\.pdf\OpenWithProgids" "${APP_PROG_ID}" ""
 WriteRegStr HKCU "${APP_REG}\Capabilities" "ApplicationName" "${APP_NAME}"
 WriteRegStr HKCU "${APP_REG}\Capabilities" "ApplicationDescription" "Open, annotate and organise PDF files."
 WriteRegStr HKCU "${APP_REG}\Capabilities\FileAssociations" ".pdf" "${APP_PROG_ID}"
 WriteRegStr HKCU "Software\RegisteredApplications" "${APP_ID}" "${APP_REG}\Capabilities"
 ; Remove only our old links when a previously selected option is turned off.
 Delete "$DESKTOP\${APP_NAME}.lnk"
 Delete "$SMPROGRAMS\${APP_NAME}.lnk"
 WriteRegDWORD HKCU "${APP_REG}" "DesktopShortcut" 0
 WriteRegDWORD HKCU "${APP_REG}" "StartMenuShortcut" 0
 System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

Section "Desktop shortcut" DesktopSection
 CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\PolyPDF.exe" "" "$INSTDIR\PolyPDF.exe" 0
 WriteRegDWORD HKCU "${APP_REG}" "DesktopShortcut" 1
SectionEnd

Section "Start menu shortcut" StartSection
 CreateShortCut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\PolyPDF.exe" "" "$INSTDIR\PolyPDF.exe" 0
 WriteRegDWORD HKCU "${APP_REG}" "StartMenuShortcut" 1
SectionEnd

Function un.onInit
 SetShellVarContext current
 SetRegView 64
 !insertmacro SetupLock
 !insertmacro UpdateGuard
 !insertmacro RunningGuard
 ReadINIStr $0 "$INSTDIR\polypdf-installation.ini" "PolyPDF" "AppId"
 ${If} $0 != "${APP_ID}"
  !insertmacro Reject "This folder does not contain a managed PolyPDF installation. No files were removed."
 ${EndIf}
 ReadRegStr $RegisteredDir HKCU "${APP_REG}" "InstallDir"
 ${If} $RegisteredDir != $INSTDIR
  !insertmacro Reject "The registered installation folder differs. No files were removed."
 ${EndIf}
FunctionEnd

Section "Uninstall"
 !insertmacro RunningGuard
 !include "${OWNED_FILES}"
 Delete "$INSTDIR\polypdf-installation.ini"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 Delete "$DESKTOP\${APP_NAME}.lnk"
 Delete "$SMPROGRAMS\${APP_NAME}.lnk"
 ReadRegStr $0 HKCU "Software\Classes\Applications\${APP_FILE_ID}\shell\open\command" ""
 ${If} $0 == '${OPEN_COMMAND}'
  DeleteRegKey HKCU "Software\Classes\Applications\${APP_FILE_ID}"
 ${EndIf}
 ReadRegStr $0 HKCU "Software\Classes\${APP_PROG_ID}\shell\open\command" ""
 ${If} $0 == '${OPEN_COMMAND}'
  DeleteRegKey HKCU "Software\Classes\${APP_PROG_ID}"
  DeleteRegValue HKCU "Software\Classes\.pdf\OpenWithProgids" "${APP_PROG_ID}"
 ${EndIf}
 DeleteRegValue HKCU "Software\RegisteredApplications" "${APP_ID}"
 DeleteRegKey HKCU "${UNINSTALL_REG}"
 DeleteRegKey HKCU "${APP_REG}"
 System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

Function .onInit
 SetShellVarContext current
 SetRegView 64
 ${IfNot} ${RunningX64}
  !insertmacro Reject "PolyPDF requires 64-bit Windows."
 ${EndIf}
 ${IfNot} ${AtLeastWin10}
  !insertmacro Reject "PolyPDF requires Windows 10 version 2004 or newer."
 ${EndIf}
 ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
 ${If} $0 < 19041
  !insertmacro Reject "PolyPDF requires Windows 10 version 2004 or newer."
 ${EndIf}
 !insertmacro SetupLock
 !ifndef MAINTENANCE_ONLY
  !insertmacro UpdateGuard
 !else
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\PolyPDF.Update") p.r0'
  ${If} $0 == 0
   !insertmacro Reject "This package is used only by PolyPDF's in-app updater. Run the conventional setup to install PolyPDF."
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r0)'
 !endif
 !insertmacro RunningGuard
 ReadRegStr $RegisteredDir HKCU "${APP_REG}" "InstallDir"
 ReadRegStr $0 HKCU "${UNINSTALL_REG}" "DisplayVersion"
 ${If} $0 != ""
  ${VersionCompare} "$0" "${APP_VERSION}" $1
  ${If} $1 == 1
   !insertmacro Reject "A newer PolyPDF version is already installed. Setup will not downgrade it."
  ${EndIf}
 ${EndIf}
 ; Restore prior shortcut choices, with command-line overrides for automation.
 ReadRegDWORD $Choice HKCU "${APP_REG}" "DesktopShortcut"
 ${If} $Choice == 0
 ${AndIfNot} ${Errors}
  !insertmacro UnselectSection ${DesktopSection}
 ${EndIf}
 ClearErrors
 ReadRegDWORD $Choice HKCU "${APP_REG}" "StartMenuShortcut"
 ${If} $Choice == 0
 ${AndIfNot} ${Errors}
  !insertmacro UnselectSection ${StartSection}
 ${EndIf}
 ${GetParameters} $Parameters
 ClearErrors
 ${GetOptions} $Parameters "/DESKTOP=" $Choice
 ${IfNot} ${Errors}
  ${If} $Choice == 0
   !insertmacro UnselectSection ${DesktopSection}
  ${Else}
   !insertmacro SelectSection ${DesktopSection}
  ${EndIf}
 ${EndIf}
 ClearErrors
 ${GetOptions} $Parameters "/STARTMENU=" $Choice
 ${IfNot} ${Errors}
  ${If} $Choice == 0
   !insertmacro UnselectSection ${StartSection}
  ${Else}
   !insertmacro SelectSection ${StartSection}
  ${EndIf}
 ${EndIf}
FunctionEnd

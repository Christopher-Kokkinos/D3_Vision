Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "LogicLib.nsh"
Name "D3Vision"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\D3Vision"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
!ifdef VALIDATION_BUILD
SetCompress off
!endif
ManifestDPIAware True
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "D3Vision"
VIAddVersionKey /LANG=1033 "FileDescription" "D3Vision Windows Installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "D3Vision"
!define MUI_ABORTWARNING
!define MUI_ICON "${PACKAGE_DIR}\Assets\D3Vision.ico"
!define MUI_UNICON "${PACKAGE_DIR}\Assets\D3Vision.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\D3Vision.UI.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch D3Vision"
!define MUI_FINISHPAGE_TEXT "D3Vision has been installed.$\r$\n$\r$\nF10: overlay visibility$\r$\nF11: click-through/editing$\r$\nF12: settings$\r$\n$\r$\nUse Diablo III Windowed (Fullscreen) or Windowed mode."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!define MUI_PAGE_CUSTOMFUNCTION_PRE CheckApplicationClosed
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

!macro CheckRunning PREFIX
Function ${PREFIX}CheckApplicationClosed
!ifndef VALIDATION_BUILD
 retry:
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\D3Vision.Desktop") p.r0'
 ${If} $0 <> 0
  System::Call 'kernel32::CloseHandle(p r0)'
  IfSilent silent_block
  MessageBox MB_RETRYCANCEL|MB_ICONINFORMATION "Close D3Vision using F12 > Exit D3Vision, then click Retry. Settings and statistics will be kept." IDRETRY retry
  Abort
 silent_block:
  SetErrorLevel 2
  Quit
 ${EndIf}
!endif
FunctionEnd
!macroend
!insertmacro CheckRunning ""
!insertmacro CheckRunning "un."

Function .onInit
 SetShellVarContext current
 SetRegView 64
 ${IfNot} ${IsNativeAMD64}
  MessageBox MB_OK|MB_ICONSTOP "This installer requires Intel/AMD 64-bit Windows." /SD IDOK
  SetErrorLevel 3
  Quit
 ${EndIf}
 ${IfNot} ${AtLeastBuild} 19041
  MessageBox MB_OK|MB_ICONSTOP "D3Vision requires Windows 10 version 2004 (build 19041) or newer, or Windows 11." /SD IDOK
  SetErrorLevel 3
  Quit
 ${EndIf}
!ifndef VALIDATION_BUILD
 ReadRegStr $0 HKCU "Software\D3Vision" "InstallDir"
 ${If} $0 != ""
  StrCpy $INSTDIR $0
 ${EndIf}
!endif
FunctionEnd

Section "D3Vision (required)" Main
 SectionIn RO
 Call CheckApplicationClosed
 SetOutPath "$INSTDIR"
 File /r "${PUBLISH_DIR}\*.*"
 WriteINIStr "$INSTDIR\installation.ini" "D3Vision" "AppId" "D3Vision.Desktop"
 WriteINIStr "$INSTDIR\installation.ini" "D3Vision" "InstallDir" "$INSTDIR"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
!ifndef VALIDATION_BUILD
 CreateDirectory "$SMPROGRAMS\D3Vision"
 CreateShortcut "$SMPROGRAMS\D3Vision\D3Vision.lnk" "$INSTDIR\D3Vision.UI.exe"
 CreateShortcut "$SMPROGRAMS\D3Vision\Uninstall D3Vision.lnk" "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "Software\D3Vision" "InstallDir" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "DisplayName" "D3Vision"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "DisplayVersion" "${APP_VERSION}"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "Publisher" "D3Vision"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "DisplayIcon" "$INSTDIR\D3Vision.UI.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "EstimatedSize" ${INSTALL_SIZE_KB}
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision" "NoRepair" 1
!endif
SectionEnd

Section /o "Desktop shortcut" Desktop
!ifndef VALIDATION_BUILD
 CreateShortcut "$DESKTOP\D3Vision.lnk" "$INSTDIR\D3Vision.UI.exe"
!endif
SectionEnd

Function un.onInit
 SetShellVarContext current
 SetRegView 64
 ReadINIStr $0 "$INSTDIR\installation.ini" "D3Vision" "AppId"
 ReadINIStr $1 "$INSTDIR\installation.ini" "D3Vision" "InstallDir"
 ${If} $0 != "D3Vision.Desktop"
 ${OrIf} $1 != $INSTDIR
  MessageBox MB_OK|MB_ICONSTOP "Installation marker does not match this directory. Uninstall stopped." /SD IDOK
  SetErrorLevel 4
  Abort
 ${EndIf}
 Call un.CheckApplicationClosed
FunctionEnd

Section "Uninstall"
!include "${UNINSTALL_INCLUDE}"
 Delete "$INSTDIR\installation.ini"
 Delete "$INSTDIR\Uninstall.exe"
!ifndef VALIDATION_BUILD
 Delete "$SMPROGRAMS\D3Vision\D3Vision.lnk"
 Delete "$SMPROGRAMS\D3Vision\Uninstall D3Vision.lnk"
 RMDir "$SMPROGRAMS\D3Vision"
 Delete "$DESKTOP\D3Vision.lnk"
 DeleteRegKey HKCU "Software\D3Vision"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\D3Vision"
!endif
 ; No recursive deletion: retain user-added files and all LocalAppData\D3Vision settings/history.
 RMDir "$INSTDIR"
SectionEnd

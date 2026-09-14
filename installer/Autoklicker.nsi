; Autoklicker – NSIS installer
; Per-user installation: no administrator rights and no system-wide changes.
Unicode True
RequestExecutionLevel user

!include "MUI2.nsh"
!include "LogicLib.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef RELEASE_DIR
  !define RELEASE_DIR "artifacts\release"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "artifacts\Autoklicker-Setup-${APP_VERSION}-x64.exe"
!endif
!define APP_NAME "Autoklicker"
!define APP_PUBLISHER "Autoklicker"
!define APP_EXE "Autoklicker.exe"
!define APP_REGKEY "Software\Autoklicker"
!define UNINSTALL_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Autoklicker"
!define ICON_FILE "${RELEASE_DIR}\..\..\src\Autoklicker\Assets\Autoklicker.ico"

Name "${APP_NAME}"
Caption "${APP_NAME} ${APP_VERSION} – Installation"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Autoklicker"
InstallDirRegKey HKCU "${APP_REGKEY}" "InstallDir"
ShowInstDetails nevershow
ShowUninstDetails nevershow
BrandingText "${APP_NAME}"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "Kompakter Autoklicker für Windows"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 nolongkisses - MIT License"
VIAddVersionKey "OriginalFilename" "Autoklicker-Setup-${APP_VERSION}-x64.exe"

!define MUI_ABORTWARNING
!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP_NOSTRETCH
!define MUI_WELCOMEPAGE_TITLE "${APP_NAME} installieren"
!define MUI_WELCOMEPAGE_TEXT "Der kompakte Autoklicker wird nur für dein Benutzerkonto installiert."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "${APP_NAME} starten"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApplication

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "German"

Function .onInit
  SetShellVarContext current
FunctionEnd

Function un.onInit
  SetShellVarContext current
FunctionEnd

Function LaunchApplication
  ExecShell "open" "$INSTDIR\${APP_EXE}"
FunctionEnd

Section "${APP_NAME}" SecMain
  SectionIn RO
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${RELEASE_DIR}\*.*"

  ; Keep the existing settings in %LOCALAPPDATA%\Autoklicker. The installer
  ; only owns the application directory and its shortcuts.
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${APP_REGKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTALL_REGKEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegDWORD HKCU "${UNINSTALL_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_REGKEY}" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 SW_SHOWNORMAL "" "${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Deinstallieren.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Deinstallieren.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  DeleteRegKey HKCU "${UNINSTALL_REGKEY}"
  DeleteRegKey HKCU "${APP_REGKEY}"
  RMDir /r "$INSTDIR"
  ; Settings are deliberately retained for a later reinstall.
SectionEnd

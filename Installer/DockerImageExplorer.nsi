Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
Name "DockerImageExplorer 1.1"
OutFile "..\release\DockerImageExplorer-1.1-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\DockerImageExplorer"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
Icon "..\AppIcon.ico"
UninstallIcon "..\AppIcon.ico"
VIProductVersion "1.1.0.0"
VIAddVersionKey /LANG=1033 "ProductName" "DockerImageExplorer"
VIAddVersionKey /LANG=1033 "ProductVersion" "1.1"
VIAddVersionKey /LANG=1033 "FileVersion" "1.1.0.0"
VIAddVersionKey /LANG=1033 "FileDescription" "DockerImageExplorer 1.1 Setup"
VIAddVersionKey /LANG=1033 "LegalCopyright" ""
Var TestMode
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\DockerImageExplorer.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"
Function .onInit
    SetShellVarContext current
    ${IfNot} ${RunningX64}
        MessageBox MB_ICONSTOP "DockerImageExplorer requires 64-bit Windows." /SD IDOK
        SetErrorLevel 2
        Quit
    ${EndIf}
    ${IfNot} ${AtLeastWin10}
        MessageBox MB_ICONSTOP "DockerImageExplorer requires Windows 10 or later." /SD IDOK
        SetErrorLevel 2
        Quit
    ${EndIf}
    StrCpy $TestMode "0"
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/TESTMODE" $1
    ${IfNot} ${Errors}
        StrCpy $TestMode "1"
    ${EndIf}
FunctionEnd
Function un.onInit
    SetShellVarContext current
    StrCpy $TestMode "0"
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/TESTMODE" $1
    ${IfNot} ${Errors}
        StrCpy $TestMode "1"
    ${EndIf}
FunctionEnd
Section "DockerImageExplorer (required)" MainSection
    SectionIn RO
    SetOutPath "$INSTDIR"
    SetOverwrite on
    ClearErrors
    File "..\Portable\*.*"
    ${If} ${Errors}
        MessageBox MB_ICONSTOP "Installation could not finish. Close DockerImageExplorer and run Setup again." /SD IDOK
        SetErrorLevel 1
        Abort
    ${EndIf}
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    ${If} $TestMode != "1"
        CreateDirectory "$SMPROGRAMS\DockerImageExplorer"
        CreateShortCut "$SMPROGRAMS\DockerImageExplorer\DockerImageExplorer.lnk" "$INSTDIR\DockerImageExplorer.exe" "" "$INSTDIR\DockerImageExplorer.exe"
        CreateShortCut "$SMPROGRAMS\DockerImageExplorer\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "DisplayName" "DockerImageExplorer"
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "DisplayVersion" "1.1"
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "InstallLocation" "$INSTDIR"
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "DisplayIcon" "$INSTDIR\DockerImageExplorer.exe"
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
        WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
        WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "NoModify" 1
        WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer" "NoRepair" 1
        !include "InstallSize.nsh"
    ${EndIf}
SectionEnd
Section /o "Desktop shortcut" DesktopSection
    ${If} $TestMode != "1"
        CreateShortCut "$DESKTOP\DockerImageExplorer.lnk" "$INSTDIR\DockerImageExplorer.exe" "" "$INSTDIR\DockerImageExplorer.exe"
    ${EndIf}
SectionEnd
Section "Uninstall"
    !include "UninstallFiles.nsh"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    ${If} $TestMode != "1"
        Delete "$SMPROGRAMS\DockerImageExplorer\DockerImageExplorer.lnk"
        Delete "$SMPROGRAMS\DockerImageExplorer\Uninstall.lnk"
        RMDir "$SMPROGRAMS\DockerImageExplorer"
        Delete "$DESKTOP\DockerImageExplorer.lnk"
        DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DockerImageExplorer"
    ${EndIf}
SectionEnd

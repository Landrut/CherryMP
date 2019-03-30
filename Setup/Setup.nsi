;NSIS Modern User Interface
;Simple Native Multiplayer Installer

  !include "MUI2.nsh"

  Name "Cherry Multiplayer"
  OutFile "CherryMP Install.exe"

  InstallDir "C:\Cherry Multiplayer"

  RequestExecutionLevel admin

  !define MUI_ABORTWARNING
  
  !insertmacro MUI_PAGE_LICENSE "License.txt"
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES
  
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES
  !insertmacro MUI_LANGUAGE "Russian"

Section "Client" SecDummy

  SetOutPath "$INSTDIR"

${If} ${FileExists} "$INSTDIR\*"
     RMDir /r "$INSTDIR"
${EndIf}

  File /r "C:\Cherry Multiplayer Prod\*"

  CreateShortCut "$DESKTOP\CherryMP.lnk" "$INSTDIR\CherryMP.exe" ""
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V" "CherryMPInstallDir" "$INSTDIR\"

SectionEnd

Section "Uninstall"

  Delete "$INSTDIR\Uninstall.exe"
  Delete "$DESKTOP\CherryMP.lnk"
  RMDir /r /REBOOTOK "$INSTDIR"
  DeleteRegKey /ifempty HKLM "HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V\CherryMPInstallDir"

SectionEnd
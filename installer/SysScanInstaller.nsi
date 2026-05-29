; NSIS installer script for SysScanNative
Name "SysScanNative Installer"
OutFile "SysScanNativeInstaller-1.0.exe"
InstallDir "$PROGRAMFILES\\SysScanNative"
RequestExecutionLevel admin

Section "Install"
  SetOutPath "$INSTDIR"
  ; Copy published files
  File /r "${{CD}}\\publish\\*"

  ; Create Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\\SysScanNative"
  CreateShortCut "$SMPROGRAMS\\SysScanNative\\SysScanNative.lnk" "$INSTDIR\\SysScanNative.exe"

  WriteUninstaller "$INSTDIR\\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\\SysScanNative.exe"
  Delete "$INSTDIR\\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\\SysScanNative\\SysScanNative.lnk"
  RMDir "$SMPROGRAMS\\SysScanNative"
SectionEnd

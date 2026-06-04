; SPDX-License-Identifier: MIT
; Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

#ifndef SourceDir
#define SourceDir "..\..\bin\Release\net10.0\publish\win-x64"
#endif

#ifndef OutputDir
#define OutputDir "."
#endif

#ifndef AppVersion
#define AppVersion "0.1.0-dev"
#endif

#ifndef IconFile
#define IconFile "..\..\Assets\app.ico"
#endif

#ifndef AppMutexName
#define AppMutexName "CottonSyncDesktop_B671C18E_1E77_437C_AB9B_5C5C9D877E18"
#endif

[Setup]
AppId={{B671C18E-1E77-437C-AB9B-5C5C9D877E18}
AppName=Cotton Sync
AppVersion={#AppVersion}
AppPublisher=Belov
AppPublisherURL=https://cottoncloud.dev
AppSupportURL=https://cottoncloud.dev
DefaultDirName={localappdata}\Programs\Cotton Sync
DefaultGroupName=Cotton Sync
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=cotton-sync-desktop-win-x64-setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\Cotton.Sync.Desktop.exe
AppMutex={#AppMutexName}
CloseApplications=yes
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Cotton Sync"; Filename: "{app}\Cotton.Sync.Desktop.exe"; IconFilename: "{app}\Cotton.Sync.Desktop.exe"
Name: "{group}\Uninstall Cotton Sync"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Cotton Sync"; Filename: "{app}\Cotton.Sync.Desktop.exe"; IconFilename: "{app}\Cotton.Sync.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Cotton.Sync.Desktop.exe"; Description: "Launch Cotton Sync"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Cotton Sync');
  end;
end;

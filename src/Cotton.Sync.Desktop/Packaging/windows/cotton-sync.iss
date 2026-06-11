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

#ifndef OutputBaseFilename
#define OutputBaseFilename "cotton-sync-desktop-win-x64-setup"
#endif

#ifndef IconFile
#define IconFile "..\..\Assets\app.ico"
#endif

#ifndef AppMutexName
#define AppMutexName "CottonSyncDesktop_B671C18E_1E77_437C_AB9B_5C5C9D877E18"
#endif

#ifndef AppUserModelId
#define AppUserModelId "Cotton.Sync.Desktop"
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
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\Cotton.Sync.Desktop.exe
AppMutex={#AppMutexName}
CloseApplications=force
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Cotton Sync"; Filename: "{app}\Cotton.Sync.Desktop.exe"; IconFilename: "{app}\Cotton.Sync.Desktop.exe"; AppUserModelID: "{#AppUserModelId}"
Name: "{group}\Uninstall Cotton Sync"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Cotton Sync"; Filename: "{app}\Cotton.Sync.Desktop.exe"; IconFilename: "{app}\Cotton.Sync.Desktop.exe"; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Cotton Sync"; ValueData: """{app}\Cotton.Sync.Desktop.exe"" --start-minimized"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Cotton.Sync.Desktop.exe"; Description: "Launch Cotton Sync"; Flags: nowait postinstall skipifsilent

[Code]
function PowerShellSingleQuotedLiteral(Value: String): String;
begin
  StringChangeEx(Value, '''', '''''', True);
  Result := '''' + Value + '''';
end;

procedure StopInstalledAppForSilentUninstall();
var
  ResultCode: Integer;
  PowerShellPath: String;
  AppExecutablePath: String;
  Command: String;
begin
  if not UninstallSilent then
  begin
    exit;
  end;

  AppExecutablePath := ExpandConstant('{app}\Cotton.Sync.Desktop.exe');
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Command := '$target = ' + PowerShellSingleQuotedLiteral(AppExecutablePath) + '; ' +
    'Get-CimInstance Win32_Process | ' +
    'Where-Object { $_.Name -eq ''Cotton.Sync.Desktop.exe'' -and $_.ExecutablePath -eq $target } | ' +
    'ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Wait-Process -Id $_.ProcessId -Timeout 5 -ErrorAction SilentlyContinue }';

  if Exec(PowerShellPath, '-NoProfile -ExecutionPolicy Bypass -Command ' + AddQuotes(Command), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log(Format('Silent uninstall pre-close command exited with code %d.', [ResultCode]));
  end
  else
  begin
    Log('Silent uninstall pre-close command could not be started.');
  end;
end;

function InitializeUninstall(): Boolean;
begin
  StopInstalledAppForSilentUninstall();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Cotton Sync');
  end;
end;

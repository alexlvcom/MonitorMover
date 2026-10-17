#ifndef AppVersion
#define AppVersion "1.2.0"
#endif

[Setup]
AppId={{F2C83907-88D1-4A59-BF26-6EBDB61C2C5E}
AppName=Monitor Mover
AppVersion={#AppVersion}
AppPublisher=alexlvcom
AppPublisherURL=https://github.com/alexlvcom/MonitorMover
AppSupportURL=https://github.com/alexlvcom/MonitorMover/issues
DefaultDirName={autopf}\Monitor Mover
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MonitorMover-Setup
SetupIconFile=..\MonitorMover.ico
UninstallDisplayIcon={app}\MonitorMover.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: startup; Description: "Start with Windows"; Flags: unchecked

[Files]
Source: "..\publish\installer\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{autoprograms}\MonitorMover.lnk"
Type: files; Name: "{autodesktop}\MonitorMover.lnk"
Type: files; Name: "{autostartup}\MonitorMover.lnk"
Type: files; Name: "{userprograms}\MonitorMover.lnk"
Type: files; Name: "{userdesktop}\MonitorMover.lnk"
Type: files; Name: "{userstartup}\MonitorMover.lnk"
Type: files; Name: "{commonprograms}\MonitorMover.lnk"
Type: files; Name: "{commondesktop}\MonitorMover.lnk"
Type: files; Name: "{commonstartup}\MonitorMover.lnk"

[Icons]
Name: "{autoprograms}\Monitor Mover"; Filename: "{app}\MonitorMover.exe"
Name: "{autodesktop}\Monitor Mover"; Filename: "{app}\MonitorMover.exe"; Tasks: desktopicon
Name: "{autostartup}\Monitor Mover"; Filename: "{app}\MonitorMover.exe"; Tasks: startup

[Run]
Filename: "{app}\MonitorMover.exe"; Flags: nowait shellexec; Check: RestartAfterUpgrade
Filename: "{app}\MonitorMover.exe"; Description: "Start Monitor Mover"; Flags: nowait postinstall skipifsilent shellexec; Check: OfferFirstLaunch

[Code]
var
  WasRunning: Boolean;

function RestartAfterUpgrade: Boolean;
begin
  Result := WasRunning;
end;

function OfferFirstLaunch: Boolean;
begin
  Result := not WasRunning;
end;

function StopApplication: Boolean;
var
  AppWindow: HWND;
  Attempt: Integer;
begin
  Result := not CheckForMutexes('MonitorMover.Singleton');
  if Result then
    Exit;

  Log('Requesting application shutdown.');
  AppWindow := FindWindowByWindowName('MonitorMoverListener');
  if AppWindow <> 0 then
    PostMessage(AppWindow, $0010, 0, 0); { WM_CLOSE: clean exit }

  for Attempt := 1 to 100 do
  begin
    if not CheckForMutexes('MonitorMover.Singleton') then
    begin
      Result := True;
      Exit;
    end;
    Sleep(100);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  WasRunning := CheckForMutexes('MonitorMover.Singleton');
  if WasRunning and not StopApplication then
    Result := 'Monitor Mover is running. Please close it before running setup.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usAppMutexCheck then
  begin
    if not StopApplication then
      RaiseException('Monitor Mover is running. Please close it before uninstalling.');
  end;
end;

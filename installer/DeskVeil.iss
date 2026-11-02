; DeskVeil installer (Inno Setup 6). Per-user install, no administrator rights, no UAC prompt.
;
; Build (see .github/workflows/build.yml):
;   dotnet publish src/DeskVeil/DeskVeil.csproj -c Release -r win-x64 --self-contained true -p:Version=0.2.0 -o artifacts/app
;   ISCC.exe /DAppVersion=0.2.0 /DSourceDir=..\artifacts\app /DOutputDir=..\artifacts installer\DeskVeil.iss
;
; Names shared with the app (keep in sync):
;   - mutex  Local\DeskVeil.SingleInstance   (src/DeskVeil/SingleInstance.cs)
;   - window DeskVeil.ControlWindow           (src/DeskVeil/ControlWindow.cs)
;   - HKCU\Software\Microsoft\Windows\CurrentVersion\Run, value "DeskVeil" = "<path to DeskVeil.exe>"
;     with quotes (src/DeskVeil/Config/Autostart.cs)

#ifndef AppVersion
  #error Pass the version on the command line, e.g. ISCC /DAppVersion=0.2.0 installer\DeskVeil.iss
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

; VersionInfoVersion only takes numbers: "0.2.0-ci.12" becomes "0.2.0".
#define DashPos Pos("-", AppVersion)
#if DashPos > 0
  #define AppNumericVersion Copy(AppVersion, 1, DashPos - 1)
#else
  #define AppNumericVersion AppVersion
#endif

[Setup]
; Never change AppId: upgrades and the uninstaller entry are matched on it.
AppId={{6685DFB6-B1FA-43AA-877C-2BFD47A6A589}
AppName=DeskVeil
AppVersion={#AppVersion}
AppVerName=DeskVeil {#AppVersion}
AppPublisher=dB Brothers
AppPublisherURL=https://github.com/Tommy-db-brothers/privacy-desktops-windows
AppSupportURL=https://github.com/Tommy-db-brothers/privacy-desktops-windows/issues
AppUpdatesURL=https://github.com/Tommy-db-brothers/privacy-desktops-windows/releases
AppCopyright=Copyright (c) 2026 dB Brothers
VersionInfoVersion={#AppNumericVersion}
VersionInfoProductVersion={#AppNumericVersion}
VersionInfoDescription=DeskVeil Setup
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\DeskVeil
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11 24H2; DeskVeil itself checks the exact build (26100.2605+) and explains when it is too old.
MinVersion=10.0.26100
LicenseFile=..\LICENSE
SetupIconFile=..\src\DeskVeil\DeskVeil.ico
UninstallDisplayIcon={app}\DeskVeil.exe
UninstallDisplayName=DeskVeil
OutputDir={#OutputDir}
OutputBaseFilename=DeskVeil-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
; DeskVeil is closed by [Code] below before files are replaced.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
dutch.AutoStartTask=Start met Windows
dutch.AutoStartGroup=Opstarten:
dutch.LaunchNow=DeskVeil nu starten
dutch.CouldNotClose=DeskVeil draait nog en kon niet automatisch worden afgesloten. Sluit DeskVeil via het tray-icoon (Afsluiten) en probeer het opnieuw.
dutch.RemoveSettings=Wil je ook je DeskVeil-instellingen verwijderen (welke bureaubladen vergrendeld zijn)?%n%n%1
english.AutoStartTask=Start with Windows
english.AutoStartGroup=Startup:
english.LaunchNow=Start DeskVeil now
english.CouldNotClose=DeskVeil is still running and could not be closed automatically. Exit DeskVeil from its tray icon (Afsluiten) and try again.
english.RemoveSettings=Do you also want to remove your DeskVeil settings (which desktops are locked)?%n%n%1

[Tasks]
Name: "autostart"; Description: "{cm:AutoStartTask}"; GroupDescription: "{cm:AutoStartGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DeskVeil"; Filename: "{app}\DeskVeil.exe"

[Run]
Filename: "{app}\DeskVeil.exe"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Logs are diagnostics, not user data. Settings in %APPDATA%\DeskVeil are handled in [Code].
Type: filesandordirs; Name: "{localappdata}\DeskVeil\logs"
Type: dirifempty; Name: "{localappdata}\DeskVeil"

[Code]
const
  InstanceMutex = 'Local\DeskVeil.SingleInstance';
  ControlWindowTitle = 'DeskVeil.ControlWindow';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'DeskVeil';
  WM_CLOSE = $0010;
  CloseTimeoutMs = 10000;

{ Asks a running DeskVeil to exit (WM_CLOSE to its control window) and waits until its
  single-instance mutex is gone. Returns True when DeskVeil is not running (any more). }
function CloseRunningDeskVeil(): Boolean;
var
  Wnd: HWND;
  Waited: Integer;
begin
  Result := True;
  if not CheckForMutexes(InstanceMutex) then
    Exit;

  Log('DeskVeil is running; asking it to exit.');
  Wnd := FindWindowByWindowName(ControlWindowTitle);
  if Wnd <> 0 then
    PostMessage(Wnd, WM_CLOSE, 0, 0);

  Waited := 0;
  while CheckForMutexes(InstanceMutex) and (Waited < CloseTimeoutMs) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;

  Result := not CheckForMutexes(InstanceMutex);
  if Result then
    Log('DeskVeil exited.')
  else
    Log('DeskVeil did not exit in time.');
end;

function AutostartCommand(): String;
begin
  Result := '"' + ExpandConstant('{app}\DeskVeil.exe') + '"';
end;

{ Install and upgrade: close a running DeskVeil before files are replaced. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseRunningDeskVeil() then
    Result := CustomMessage('CouldNotClose');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Same value the tray toggle writes, so the two stay in sync. An unticked box leaves an
    existing entry alone: it may have been switched on from the tray menu. }
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('autostart') then
    RegWriteStringValue(HKEY_CURRENT_USER, RunKey, RunValue, AutostartCommand());
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseRunningDeskVeil();
  if not Result then
    MsgBox(CustomMessage('CouldNotClose'), mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: String;
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, RunValue);

  if CurUninstallStep = usPostUninstall then
  begin
    SettingsDir := ExpandConstant('{userappdata}\DeskVeil');
    { Default is to keep the settings; silent uninstalls never remove them. }
    if DirExists(SettingsDir) and not UninstallSilent() then
      if MsgBox(FmtMessage(CustomMessage('RemoveSettings'), [SettingsDir]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(SettingsDir, True, True, True);
  end;
end;

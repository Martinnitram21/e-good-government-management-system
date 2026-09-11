; ============================================================================
; Good Governance Management System — Inno Setup Installer
; ============================================================================
; SYNC SOURCE (single source of truth — keep these in sync):
;   1. appsettings.json
;   2. ViewModels/SettingsViewModel.cs  (Remote*/Network*/Crs* defaults)
;   3. Data/DatabaseConfig.cs           (fallback connection strings)
;   4. App.xaml.cs                      (defaultJson fallback)
;   5. THIS FILE (prefilled wizard textbox defaults below)
;
; If you change DB credentials anywhere, update ALL FIVE places.
;
; Flow:
;   Installer wizard textboxes (prefilled below)
;     -> writes {app}\appsettings.json on install (CurStepChanged)
;     -> also writes {userappdata}\GoodGovernanceApp\appsettings.json so an
;        older per-user configuration cannot hide the newly confirmed values
;     -> SettingsViewModel.LoadSettings() displays the same values in-app.
; ============================================================================

#define MyAppName "Good Governance Management System"
#define MyAppShortName "GoodGovernanceSystem"
#define MyAppVersion "1.0.8"
#define MyAppPublisher "Good Governance System"
#define MyAppExeName "GoodGovernanceApp.exe"

; ---- PREFILLED DEFAULTS — MUST MATCH appsettings.json / SettingsViewModel ----
; Online (Remote / main cloud DB)
#define RemoteServer "194.59.164.58"
#define RemotePort "3306"
#define RemoteDatabase "u621755393_ggms"
#define RemoteUser "u621755393_ggms_user"
#define RemotePassword "Ggms@2026"

; Network (Office LAN — GGMS database)
#define NetworkServer "192.168.0.47"
#define NetworkPort "3306"
#define NetworkDatabase "ggms_db"
#define NetworkUser "root"
#define NetworkPassword "network@2026"

; CRS Database (Office LAN — separate database, same server)
#define CrsServer "192.168.0.47"
#define CrsPort "3306"
#define CrsDatabase "crs_db"
#define CrsUser "root"
#define CrsPassword "network@2026"

[Setup]
AppId={{3B4C8A1E-7D2F-4A9B-9C5E-GGMS2026SETUP}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
; Installer exe is built to .\InstallerOutput, then build_installer.ps1
; copies it straight to the user's Downloads folder
OutputDir=.\InstallerOutput
OutputBaseFilename=GoodGovernanceSetup-{#MyAppVersion}
SetupIconFile=Assets\Images\applicationlogo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Published self-contained multi-file output (run build_installer.ps1 first).
; NOTE: PublishSingleFile is intentionally NOT used — single-file bundles break
; WPF pack://application resources (bg image / icons / MaterialDesign) with a
; "TypeConverterMarkupExtension ... path1 null" crash on startup.
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Fallback config — always installed, then OVERWRITTEN with wizard values in [Code]
Source: "appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  OnlinePage: TInputQueryWizardPage;
  NetworkPage: TInputQueryWizardPage;
  CrsPage: TInputQueryWizardPage;

{ Escape backslashes and quotes for safe JSON embedding }
function JsonEscape(const S: String): String;
var
  I: Integer;
  C: Char;
begin
  Result := '';
  for I := 1 to Length(S) do
  begin
    C := S[I];
    if C = '\' then
      Result := Result + '\\'
    else if C = '"' then
      Result := Result + '\"'
    else if C = #13 then
      { skip CR }
    else if C = #10 then
      Result := Result + '\n'
    else if C = #9 then
      Result := Result + '\t'
    else
      Result := Result + C;
  end;
end;

function BuildConnStr(const Server, Port, Database, User, Password: String): String;
begin
  Result :=
    'Server=' + Server + ';' +
    'Port=' + Port + ';' +
    'Database=' + Database + ';' +
    'User=' + User + ';' +
    'Password=' + Password + ';' +
    'AllowZeroDateTime=True;ConvertZeroDateTime=True;';
end;

procedure InitializeWizard;
begin
  { ---- Page 1: Online (Remote) database ---- }
  OnlinePage := CreateInputQueryPage(wpSelectDir,
    'Online Database (Remote)',
    'Confirm the cloud database credentials.',
    'These are prefilled with the production defaults. ' +
    'Change them only if the online server moved. ' +
    'The same values appear in the app under Settings.');
  OnlinePage.Add('Server:', False);
  OnlinePage.Add('Port:', False);
  OnlinePage.Add('Database:', False);
  OnlinePage.Add('User:', False);
  OnlinePage.Add('Password:', True);
  OnlinePage.Values[0] := '{#RemoteServer}';
  OnlinePage.Values[1] := '{#RemotePort}';
  OnlinePage.Values[2] := '{#RemoteDatabase}';
  OnlinePage.Values[3] := '{#RemoteUser}';
  OnlinePage.Values[4] := '{#RemotePassword}';

  { ---- Page 2: Network (Office LAN) GGMS database ---- }
  NetworkPage := CreateInputQueryPage(OnlinePage.ID,
    'Network Database (Office LAN)',
    'Confirm the office-network GGMS database credentials.',
    'Prefilled with the office LAN defaults. ' +
    'Must match Settings > Network (LAN) in the app.');
  NetworkPage.Add('Server:', False);
  NetworkPage.Add('Port:', False);
  NetworkPage.Add('Database:', False);
  NetworkPage.Add('User:', False);
  NetworkPage.Add('Password:', True);
  NetworkPage.Values[0] := '{#NetworkServer}';
  NetworkPage.Values[1] := '{#NetworkPort}';
  NetworkPage.Values[2] := '{#NetworkDatabase}';
  NetworkPage.Values[3] := '{#NetworkUser}';
  NetworkPage.Values[4] := '{#NetworkPassword}';

  { ---- Page 3: CRS database (LAN, separate DB) ---- }
  CrsPage := CreateInputQueryPage(NetworkPage.ID,
    'CRS Database (Office LAN)',
    'Confirm the CRS database credentials.',
    'Prefilled with the CRS defaults (same LAN server, separate database). ' +
    'Must match Settings > CRS Database in the app.');
  CrsPage.Add('Server:', False);
  CrsPage.Add('Port:', False);
  CrsPage.Add('Database:', False);
  CrsPage.Add('User:', False);
  CrsPage.Add('Password:', True);
  CrsPage.Values[0] := '{#CrsServer}';
  CrsPage.Values[1] := '{#CrsPort}';
  CrsPage.Values[2] := '{#CrsDatabase}';
  CrsPage.Values[3] := '{#CrsUser}';
  CrsPage.Values[4] := '{#CrsPassword}';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  { Basic validation: server + database must not be empty }
  if CurPageID = OnlinePage.ID then
  begin
    if (Trim(OnlinePage.Values[0]) = '') or (Trim(OnlinePage.Values[2]) = '') then
    begin
      MsgBox('Online Database: Server and Database cannot be empty.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = NetworkPage.ID then
  begin
    if (Trim(NetworkPage.Values[0]) = '') or (Trim(NetworkPage.Values[2]) = '') then
    begin
      MsgBox('Network Database: Server and Database cannot be empty.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = CrsPage.ID then
  begin
    if (Trim(CrsPage.Values[0]) = '') or (Trim(CrsPage.Values[2]) = '') then
    begin
      MsgBox('CRS Database: Server and Database cannot be empty.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// Write (app)\appsettings.json from the wizard textbox values so the
// installed config is in sync with what the user confirmed at setup.
procedure WriteAppsettingsFromWizard;
var
  InstallPath, UserConfigDir, UserConfigPath, Json: String;
  RemoteConn, NetworkConn, LanConn, LocalConn: String;
begin
  RemoteConn := BuildConnStr(
    Trim(OnlinePage.Values[0]), Trim(OnlinePage.Values[1]),
    Trim(OnlinePage.Values[2]), Trim(OnlinePage.Values[3]),
    OnlinePage.Values[4]);
  NetworkConn := BuildConnStr(
    Trim(NetworkPage.Values[0]), Trim(NetworkPage.Values[1]),
    Trim(NetworkPage.Values[2]), Trim(NetworkPage.Values[3]),
    NetworkPage.Values[4]);

  { LanConnection kept as alias of NetworkConnection for backward compat }
  LanConn := NetworkConn;
  { LocalConnection fallback (unused when offline-first SQLite is active, }
  { but kept so the key always exists) }
  LocalConn := 'Server=127.0.0.1;Port=3306;Database=govern;User=root;Password=root;SslMode=None;AllowPublicKeyRetrieval=True;';

  Json :=
    '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "LocalConnection": "' + JsonEscape(LocalConn) + '",' + #13#10 +
    '    "LanConnection": "' + JsonEscape(LanConn) + '",' + #13#10 +
    '    "NetworkConnection": "' + JsonEscape(NetworkConn) + '",' + #13#10 +
    '    "RemoteConnection": "' + JsonEscape(RemoteConn) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "CrsConnection": {' + #13#10 +
    '    "Server": "' + JsonEscape(Trim(CrsPage.Values[0])) + '",' + #13#10 +
    '    "Port": "' + JsonEscape(Trim(CrsPage.Values[1])) + '",' + #13#10 +
    '    "Database": "' + JsonEscape(Trim(CrsPage.Values[2])) + '",' + #13#10 +
    '    "User": "' + JsonEscape(Trim(CrsPage.Values[3])) + '",' + #13#10 +
    '    "Password": "' + JsonEscape(CrsPage.Values[4]) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "SmtpConnection": {' + #13#10 +
    '    "EmailAddress": "bryanluy822@gmail.com",' + #13#10 +
    '    "AppPassword": "afyyknunzimihtrv"' + #13#10 +
    '  },' + #13#10 +
    '  "AppSettings": {' + #13#10 +
    '    "DatabaseMode": "Remote",' + #13#10 +
    '    "UseRemoteDatabase": true,' + #13#10 +
    '    "MySqlDumpPath": "mysqldump"' + #13#10 +
    '  }' + #13#10 +
    '}';

  { Keep the installed template and active per-user configuration identical. }
  InstallPath := ExpandConstant('{app}\appsettings.json');
  if not SaveStringToFile(InstallPath, Json, False) then
    RaiseException('Unable to write the installed database configuration.');

  UserConfigDir := ExpandConstant('{userappdata}\GoodGovernanceApp');
  if not ForceDirectories(UserConfigDir) then
    RaiseException('Unable to create the per-user configuration folder.');

  UserConfigPath := UserConfigDir + '\appsettings.json';
  if not SaveStringToFile(UserConfigPath, Json, False) then
    RaiseException('Unable to write the per-user database configuration.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteAppsettingsFromWizard;
end;

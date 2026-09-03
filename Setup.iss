; ============================================================================
;  World Time & Alarms - Inno Setup installer script
;  Compile with Inno Setup Compiler (ISCC.exe) 6.x or newer
;  https://jrsoftware.org/isinfo.php
;  Single installer script:
;  - If Installer\Redist\windowsdesktop-runtime-10.0.11-win-x64.exe exists, it is
;    bundled for fully offline installation.
;  - If it does not exist, setup downloads .NET Desktop Runtime automatically.
;  - Version is kept in sync with the application by Installer\BuildInstaller.ps1.
;  - The language selector is always shown at startup.
; ============================================================================

#define MyAppName "World Time & Alarms"
#define MyAppVersion "1.0.17"
#define MyAppPublisher "World Time & Alarms"
#define MyAppURL "https://github.com/"
#define MyAppExeName "WorldTimeAlarms.exe"
#define MyAppIcon "WorldTimeAlarms\app_logo.ico"
; Folder produced by `dotnet publish -r win-x64 --self-contained false`
#define MyPublishDir "WorldTimeAlarms\bin\Release\net10.0-windows\win-x64\publish"
#define DotNetDesktopRuntimeVersion "10.0.11"
#define DotNetDesktopRuntimeFileName "windowsdesktop-runtime-" + DotNetDesktopRuntimeVersion + "-win-x64.exe"
; Bundled locally so the installer works fully offline when present.
#define DotNetDesktopRuntimePath "Installer\Redist\" + DotNetDesktopRuntimeFileName
#define DotNetDesktopRuntimeUrl "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/" + DotNetDesktopRuntimeVersion + "/windowsdesktop-runtime-" + DotNetDesktopRuntimeVersion + "-win-x64.exe"
#define DotNetDesktopRuntimeFallbackUrl "https://aka.ms/dotnet/10/windowsdesktop-runtime-win-x64.exe"
#define HasOfflineDotNet FileExists(DotNetDesktopRuntimePath)
#define HasWizardImage FileExists("Installer\WizardImage.bmp")
#define HasWizardImageHighDpi FileExists("Installer\WizardImage-HighDPI.bmp")
#define HasWizardSmallImage FileExists("Installer\WizardSmallImage.bmp")
#define HasWizardSmallImageHighDpi FileExists("Installer\WizardSmallImage-HighDPI.bmp")
#define AppUninstallKey "{6E9B6C7B-6E9C-4B0C-9A1F-6C7D1F2A3B4D}_is1"

[Setup]
; Unique app identifier - do not change between versions
AppId={{6E9B6C7B-6E9C-4B0C-9A1F-6C7D1F2A3B4D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=InstallerOutput
OutputBaseFilename=WorldTimeAlarms-Setup-{#MyAppVersion}
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern
WizardSizePercent=120
DisableWelcomePage=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763
ShowLanguageDialog=yes

; ----------------------------------------------------------------------------
; Installer branding generated from the official app logo.
; WizardImageFile: 164x314 px - left banner
; WizardSmallImageFile: 55x58 px - top-right logo
; ----------------------------------------------------------------------------
#if HasWizardImage
#if HasWizardImageHighDpi
WizardImageFile=Installer\WizardImage.bmp,Installer\WizardImage-HighDPI.bmp
#else
WizardImageFile=Installer\WizardImage.bmp
#endif
WizardImageStretch=no
WizardImageBackColor=$FFFFFF
#endif
#if HasWizardSmallImage
#if HasWizardSmallImageHighDpi
WizardSmallImageFile=Installer\WizardSmallImage.bmp,Installer\WizardSmallImage-HighDPI.bmp
#else
WizardSmallImageFile=Installer\WizardSmallImage.bmp
#endif
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.StartupTaskDescription=Start {#MyAppName} automatically with Windows for all users
spanish.StartupTaskDescription=Iniciar {#MyAppName} automáticamente con Windows para todos los usuarios
english.StartupTaskGroup=Additional options
spanish.StartupTaskGroup=Opciones adicionales
english.InstallingDotNetRuntime=Installing .NET Desktop Runtime %1. This may take a moment...
spanish.InstallingDotNetRuntime=Instalando .NET Desktop Runtime %1. Esto puede tardar un momento...
english.DownloadPrereqTitle=Downloading required components
spanish.DownloadPrereqTitle=Descargando componentes necesarios
english.DownloadPrereqDescription=.NET Desktop Runtime will be downloaded automatically to complete the installation.
spanish.DownloadPrereqDescription=Se descargará automáticamente .NET Desktop Runtime para completar la instalación.
english.DownloadPrimaryStatus=Downloading .NET Desktop Runtime from Microsoft...
spanish.DownloadPrimaryStatus=Descargando .NET Desktop Runtime desde Microsoft...
english.DownloadFallbackStatus=Primary download failed. Trying alternate Microsoft source...
spanish.DownloadFallbackStatus=La descarga principal falló. Probando una fuente alternativa de Microsoft...
english.DotNetDownloadFailed=Could not download .NET Desktop Runtime automatically. Check your Internet connection and try again.
spanish.DotNetDownloadFailed=No se pudo descargar automáticamente .NET Desktop Runtime. Comprueba tu conexión a Internet e inténtalo de nuevo.
english.DowngradeNotAllowed=A newer version (%1) is already installed. This installer (%2) cannot be installed.
spanish.DowngradeNotAllowed=Ya hay instalada una versión más nueva (%1). Este instalador (%2) no se puede instalar.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "{cm:StartupTaskDescription}"; GroupDescription: "{cm:StartupTaskGroup}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#if HasOfflineDotNet
Source: "{#DotNetDesktopRuntimePath}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsDotNetDesktopRuntime
#endif

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{tmp}\{#DotNetDesktopRuntimeFileName}"; \
    Parameters: "/install /quiet /norestart"; \
	StatusMsg: "{cm:InstallingDotNetRuntime,{#DotNetDesktopRuntimeVersion}}"; \
    Flags: waituntilterminated skipifdoesntexist; \
	Check: NeedsDotNetDesktopRuntime
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
{ ----------------------------------------------------------------------------
	Detects whether a compatible .NET Desktop Runtime (WPF-capable) is already
	installed. If not, it uses bundled offline runtime when present, otherwise
  downloads it automatically during setup.
  ---------------------------------------------------------------------------- }
var
  DotNetDownloadPage: TDownloadWizardPage;
  ExistingDataPathValue: String;

procedure DeleteDirectoryIfExists(const DirPath: String);
begin
  if DirExists(DirPath) then
  begin
	DelTree(DirPath, True, True, True);
  end;
end;

function GetLegacyDataPath(): String;
begin
  Result := ExpandConstant('{app}\AppData');
end;

function GetCurrentDataPath(): String;
begin
  Result := ExpandConstant('{userappdata}\WorldTimeAlarms');
end;

procedure EnsureDataPathRegistry();
begin
  RegWriteStringValue(
	HKCU,
	'Software\WorldTimeAlarms',
	'DataPath',
	GetCurrentDataPath());
end;

procedure MigrateLegacyDataIfNeeded();
var
  LegacyPath: String;
  CurrentPath: String;
  CandidateDataPath: String;
begin
  CurrentPath := GetCurrentDataPath();
  LegacyPath := GetLegacyDataPath();
  CandidateDataPath := '';

  if RegQueryStringValue(HKCU, 'Software\WorldTimeAlarms', 'DataPath', ExistingDataPathValue) then
  begin
	if (ExistingDataPathValue <> '')
	  and (CompareText(ExistingDataPathValue, CurrentPath) <> 0)
	  and FileExists(AddBackslash(ExistingDataPathValue) + 'alarms.json') then
	begin
	  CandidateDataPath := ExistingDataPathValue;
	end;
  end;

  if (CandidateDataPath = '')
	and DirExists(LegacyPath)
	and FileExists(AddBackslash(LegacyPath) + 'alarms.json') then
  begin
	CandidateDataPath := LegacyPath;
  end;

  if CandidateDataPath = '' then
  begin
	EnsureDataPathRegistry();
	exit;
  end;

  ForceDirectories(CurrentPath);
  if CompareText(CandidateDataPath, CurrentPath) <> 0 then
  begin
	if not DirExists(CurrentPath) then
	  ForceDirectories(CurrentPath);

	if not DirExists(CurrentPath) then
	  exit;

	if FileExists(AddBackslash(CandidateDataPath) + 'alarms.json') then
	begin
	  if not FileExists(AddBackslash(CurrentPath) + 'alarms.json') then
		CopyFile(AddBackslash(CandidateDataPath) + 'alarms.json', AddBackslash(CurrentPath) + 'alarms.json', False);
	end;

	if FileExists(AddBackslash(CandidateDataPath) + 'alarms.json.bak') and not FileExists(AddBackslash(CurrentPath) + 'alarms.json.bak') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'alarms.json.bak', AddBackslash(CurrentPath) + 'alarms.json.bak', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'history.json') and not FileExists(AddBackslash(CurrentPath) + 'history.json') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'history.json', AddBackslash(CurrentPath) + 'history.json', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'history.json.bak') and not FileExists(AddBackslash(CurrentPath) + 'history.json.bak') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'history.json.bak', AddBackslash(CurrentPath) + 'history.json.bak', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'notifications.json') and not FileExists(AddBackslash(CurrentPath) + 'notifications.json') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'notifications.json', AddBackslash(CurrentPath) + 'notifications.json', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'notifications.json.bak') and not FileExists(AddBackslash(CurrentPath) + 'notifications.json.bak') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'notifications.json.bak', AddBackslash(CurrentPath) + 'notifications.json.bak', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'notifications_suppressed.json') and not FileExists(AddBackslash(CurrentPath) + 'notifications_suppressed.json') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'notifications_suppressed.json', AddBackslash(CurrentPath) + 'notifications_suppressed.json', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'settings.json') and not FileExists(AddBackslash(CurrentPath) + 'settings.json') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'settings.json', AddBackslash(CurrentPath) + 'settings.json', False);

	if FileExists(AddBackslash(CandidateDataPath) + 'settings.json.bak') and not FileExists(AddBackslash(CurrentPath) + 'settings.json.bak') then
	  CopyFile(AddBackslash(CandidateDataPath) + 'settings.json.bak', AddBackslash(CurrentPath) + 'settings.json.bak', False);
  end;

  EnsureDataPathRegistry();
  DeleteDirectoryIfExists(LegacyPath);
end;

function CompareVersionPart(const LeftValue, RightValue: String): Integer;
var
  LeftInt: Integer;
  RightInt: Integer;
begin
  LeftInt := StrToIntDef(LeftValue, 0);
  RightInt := StrToIntDef(RightValue, 0);

  if LeftInt < RightInt then
	Result := -1
  else if LeftInt > RightInt then
	Result := 1
  else
	Result := 0;
end;

function CompareVersionText(const LeftVersion, RightVersion: String): Integer;
var
  LeftPart: String;
  RightPart: String;
  LeftRest: String;
  RightRest: String;
  DotPos: Integer;
begin
  LeftRest := LeftVersion;
  RightRest := RightVersion;

  while (LeftRest <> '') or (RightRest <> '') do
  begin
	DotPos := Pos('.', LeftRest);
	if DotPos > 0 then
	begin
	  LeftPart := Copy(LeftRest, 1, DotPos - 1);
	  Delete(LeftRest, 1, DotPos);
	end
	else
	begin
	  LeftPart := LeftRest;
	  LeftRest := '';
	end;

	DotPos := Pos('.', RightRest);
	if DotPos > 0 then
	begin
	  RightPart := Copy(RightRest, 1, DotPos - 1);
	  Delete(RightRest, 1, DotPos);
	end
	else
	begin
	  RightPart := RightRest;
	  RightRest := '';
	end;

	Result := CompareVersionPart(LeftPart, RightPart);
	if Result <> 0 then
	  exit;
  end;

  Result := 0;
end;

function IsCompatibleWindowsDesktopRuntimeInstalled(const BasePath, MinimumVersion: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;

  if not DirExists(BasePath) then
	exit;

  if FindFirst(AddBackslash(BasePath) + '*', FindRec) then
  begin
	try
	  repeat
		if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0)
		  and (FindRec.Name <> '.')
		  and (FindRec.Name <> '..')
		  and (CompareVersionText(FindRec.Name, MinimumVersion) >= 0) then
		begin
		  Result := True;
		  exit;
		end;
	  until not FindNext(FindRec);
	finally
	  FindClose(FindRec);
	end;
  end;
end;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  InstallPath: String;
	SharedFrameworkPath: String;
begin
  Result := False;
	SharedFrameworkPath := ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App');

  if RegQueryStringValue(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost', 'Path', InstallPath) then
  begin
	Result := IsCompatibleWindowsDesktopRuntimeInstalled(
	  AddBackslash(InstallPath) + 'shared\Microsoft.WindowsDesktop.App',
	  '{#DotNetDesktopRuntimeVersion}');
  end;

  if not Result then
	  Result := IsCompatibleWindowsDesktopRuntimeInstalled(
	  SharedFrameworkPath,
	  '{#DotNetDesktopRuntimeVersion}');
end;

function NeedsDotNetDesktopRuntime(): Boolean;
begin
  Result := not IsDotNetDesktopRuntimeInstalled();
end;

function TryGetInstalledAppVersion(var InstalledVersion: String): Boolean;
var
  UninstallPath: String;
begin
  Result := False;
  InstalledVersion := '';
  UninstallPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppUninstallKey}';

  if RegQueryStringValue(HKLM64, UninstallPath, 'DisplayVersion', InstalledVersion) then
  begin
	Result := InstalledVersion <> '';
	if Result then
	  exit;
  end;

  if RegQueryStringValue(HKLM, UninstallPath, 'DisplayVersion', InstalledVersion) then
  begin
	Result := InstalledVersion <> '';
	if Result then
	  exit;
  end;

  if RegQueryStringValue(HKCU, UninstallPath, 'DisplayVersion', InstalledVersion) then
	Result := InstalledVersion <> '';
end;

procedure DownloadPrerequisite(const Url, StatusText: String);
begin
  DotNetDownloadPage.Clear;
  DotNetDownloadPage.Add(Url, '{#DotNetDesktopRuntimeFileName}', '');
  DotNetDownloadPage.Show;
  DotNetDownloadPage.SetText(
	CustomMessage('DownloadPrereqTitle'),
	StatusText);
  try
	DotNetDownloadPage.Download;
  finally
	DotNetDownloadPage.Hide;
  end;
end;

procedure DownloadDotNetDesktopRuntimeIfNeeded();
begin
  if not NeedsDotNetDesktopRuntime() then
	exit;

  try
	DownloadPrerequisite(
	  '{#DotNetDesktopRuntimeUrl}',
	  CustomMessage('DownloadPrimaryStatus'));
  except
	DownloadPrerequisite(
	  '{#DotNetDesktopRuntimeFallbackUrl}',
	  CustomMessage('DownloadFallbackStatus'));
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if NeedsDotNetDesktopRuntime() then
  begin
#if HasOfflineDotNet
	Result := '';
#else
	try
	  DownloadDotNetDesktopRuntimeIfNeeded();
	except
	  Result := CustomMessage('DotNetDownloadFailed');
	end;
#endif
  end;
end;

{ ----------------------------------------------------------------------------
  Ensure any running instance of the app is closed before installing/upgrading.
  ---------------------------------------------------------------------------- }
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
begin
  Result := True;

  if TryGetInstalledAppVersion(InstalledVersion)
	and (CompareVersionText(InstalledVersion, '{#MyAppVersion}') > 0) then
  begin
	MsgBox(
	  FmtMessage(CustomMessage('DowngradeNotAllowed'), [InstalledVersion, '{#MyAppVersion}']),
	  mbError,
	  MB_OK);
	Result := False;
  end;
end;

procedure InitializeWizard();
begin
  DotNetDownloadPage := CreateDownloadPage(
	CustomMessage('DownloadPrereqTitle'),
	CustomMessage('DownloadPrereqDescription'),
	nil);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
	// Reserved for pre-install steps (e.g., stopping services) if needed.
  end;

  if CurStep = ssPostInstall then
  begin
	MigrateLegacyDataIfNeeded();
  end;
end;
















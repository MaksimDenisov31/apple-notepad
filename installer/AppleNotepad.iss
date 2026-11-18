#define AppVersion "1.0.0"
#ifdef InstallerTest
  #define AppName "Apple Notepad Test " + InstallerTest
  #define AppId "AppleNotepad.InstallerTest." + InstallerTest
  #define OutputName "AppleNotepad-Setup-Test"
#else
  #define AppName "Apple Notepad"
  #define AppId "AppleNotepad.MaksimDenisovRussia"
  #define OutputName "AppleNotepad-Setup-" + AppVersion
#endif

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Максим Денисов
AppPublisherURL=https://github.com/MaksimDenisovRussia/apple-notepad
AppSupportURL=https://github.com/MaksimDenisovRussia/apple-notepad/issues
AppUpdatesURL=https://github.com/MaksimDenisovRussia/apple-notepad/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
UninstallDisplayIcon={app}\AppleNotepad.exe
UninstallDisplayName={#AppName}
SetupIconFile=..\assets\app.ico
OutputDir=..\installer-output
OutputBaseFilename={#OutputName}
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Установка {#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
CloseApplicationsFilter=AppleNotepad.exe
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.DesktopTask=Создать ярлык на рабочем столе
english.DesktopTask=Create a desktop shortcut
russian.LaunchApp=Запустить Apple Notepad
english.LaunchApp=Launch Apple Notepad
russian.DotNetMissing=Для Apple Notepad требуется .NET Framework 4.8 или новее.%n%nОткрыть официальную страницу Microsoft для его установки? После установки .NET запустите этот установщик снова.
english.DotNetMissing=Apple Notepad requires .NET Framework 4.8 or later.%n%nOpen the official Microsoft download page? After installing .NET, run this setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\AppleNotepad.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AppleNotepad.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\preview-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\preview-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}\Apple Notepad"; Filename: "{app}\AppleNotepad.exe"; WorkingDir: "{app}"
Name: "{userprograms}\{#AppName}\Инструкция"; Filename: "https://github.com/MaksimDenisovRussia/apple-notepad#readme"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\AppleNotepad.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AppleNotepad.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
  ErrorCode: Integer;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
  begin
    if not WizardSilent then
      if MsgBox(ExpandConstant('{cm:DotNetMissing}'), mbInformation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

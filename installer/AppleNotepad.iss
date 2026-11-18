#define AppVersion "1.1.1"
#ifdef InstallerTest
  #define AppName "Apple Notepad Test " + InstallerTest
  #define AppId "AppleNotepad.InstallerTest." + InstallerTest
  #define OutputName "AppleNotepad-Setup-Test"
  #define RegisteredName "AppleNotepad.InstallerTest." + InstallerTest
  #define RegisteredExe "AppleNotepad.InstallerTest." + InstallerTest + ".exe"
#else
  #define AppName "Apple Notepad"
  #define AppId "AppleNotepad.MaksimDenisovRussia"
  #define OutputName "AppleNotepad-Setup-" + AppVersion
  #define RegisteredName "AppleNotepad"
  #define RegisteredExe "AppleNotepad.exe"
#endif
#define TextProgId RegisteredName + ".Text"
#define CapabilitiesPath "Software\" + RegisteredName + "\Capabilities"

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
ChangesAssociations=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.DesktopTask=Создать ярлык на рабочем столе
english.DesktopTask=Create a desktop shortcut
russian.LaunchApp=Запустить Apple Notepad
english.LaunchApp=Launch Apple Notepad
russian.SetDefault=Выбрать Apple Notepad для текстовых файлов (подтвердите выбор в Windows)
english.SetDefault=Choose Apple Notepad for text files (confirm your choice in Windows)
russian.DefaultHelp=После нажатия «Готово» подтвердите Apple Notepad для .txt и .log в Windows. Если менять приложение не нужно, снимите отметку ниже.
english.DefaultHelp=After clicking Finish, confirm Apple Notepad for .txt and .log in Windows. Clear the checkbox below if you want to keep your current default app.
russian.DotNetMissing=Для Apple Notepad требуется .NET Framework 4.8 или новее.%n%nОткрыть официальную страницу Microsoft для его установки? После установки .NET запустите этот установщик снова.
english.DotNetMissing=Apple Notepad requires .NET Framework 4.8 or later.%n%nOpen the official Microsoft download page? After installing .NET, run this setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\AppleNotepad.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\AppleNotepad.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\preview-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\preview-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\menu-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\menu-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\context-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\context-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion

Source: "..\docs\controls-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\controls-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\font-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\font-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\open-file.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\settings.png"; DestDir: "{app}\docs"; Flags: ignoreversion

[Registry]
; Register a candidate, never overwrite an extension default or protected UserChoice.
; All test identifiers are unique, so verification leaves real registrations intact.
Root: HKCU; Subkey: "Software\Classes\{#TextProgId}"; ValueType: string; ValueName: ""; ValueData: "{#AppName} — Text Document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#TextProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\assets\app.ico"",0"
Root: HKCU; Subkey: "Software\Classes\{#TextProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\AppleNotepad.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\{#TextProgId}\Application"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: HKCU; Subkey: "Software\Classes\{#TextProgId}\Application"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\assets\app.ico"",0"
Root: HKCU; Subkey: "Software\Classes\Applications\{#RegisteredExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#RegisteredExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\AppleNotepad.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\{#RegisteredExe}\SupportedTypes"; ValueType: string; ValueName: ".txt"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#RegisteredExe}\SupportedTypes"; ValueType: string; ValueName: ".log"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "{#TextProgId}"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\.log\OpenWithProgids"; ValueType: string; ValueName: "{#TextProgId}"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty
Root: HKCU; Subkey: "{#CapabilitiesPath}"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "{#CapabilitiesPath}"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Plain text editor — .txt, .log"
Root: HKCU; Subkey: "{#CapabilitiesPath}"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\assets\app.ico"",0"
Root: HKCU; Subkey: "{#CapabilitiesPath}\FileAssociations"; ValueType: string; ValueName: ".txt"; ValueData: "{#TextProgId}"
Root: HKCU; Subkey: "{#CapabilitiesPath}\FileAssociations"; ValueType: string; ValueName: ".log"; ValueData: "{#TextProgId}"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#RegisteredName}"; ValueData: "{#CapabilitiesPath}"; Flags: uninsdeletevalue

[Icons]
Name: "{userprograms}\{#AppName}\Apple Notepad"; Filename: "{app}\AppleNotepad.exe"; WorkingDir: "{app}"
Name: "{userprograms}\{#AppName}\Инструкция"; Filename: "https://github.com/MaksimDenisovRussia/apple-notepad#readme"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\AppleNotepad.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AppleNotepad.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
Filename: "ms-settings:defaultapps?registeredAppUser={#RegisteredName}"; Description: "{cm:SetDefault}"; Flags: shellexec nowait postinstall skipifsilent

[Code]
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:DefaultHelp}');
end;

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

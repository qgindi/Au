; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "C# Uiscripter"
#define MyAppNameShort "Uiscripter"
#define MyAppVersion "0.6.0"
#define MyAppPublisher "Gintaras Did�galvis"
#define MyAppURL "https://www.quickmacros.com/au/help/"
#define MyAppExeName "Au.Editor.exe"

[Setup]
AppId={#MyAppName}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
UninstallDisplayName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\{#MyAppNameShort}
DefaultGroupName={#MyAppName}
OutputDir=..\..\_
OutputBaseFilename=Uiscripter
Compression=lzma/normal
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
MinVersion=0,6.1sp1
DisableProgramGroupPage=yes
AppMutex=Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ
UsePreviousGroup=False
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "Q:\app\Au\_\Setup32.dll"; DestDir: "{app}"; Flags: ignoreversion

Source: "Q:\app\Au\_\Au.Editor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Editor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Task.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Controls.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Net4.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\Au.Net4.exe.config"; DestDir: "{app}"; Flags: ignoreversion

Source: "Q:\app\Au\_\Roslyn\*.dll"; DestDir: "{app}\Roslyn"; Flags: ignoreversion
Source: "Q:\app\Au\_\64\Au.AppHost.exe"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "Q:\app\Au\_\64\AuCpp.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "Q:\app\Au\_\64\Scintilla.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "Q:\app\Au\_\64\Lexilla.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "Q:\app\Au\_\64\sqlite3.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "Q:\app\Au\_\32\Au.AppHost.exe"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "Q:\app\Au\_\32\AuCpp.dll"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "Q:\app\Au\_\32\sqlite3.dll"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "Q:\app\Au\_\Default\*"; DestDir: "{app}\Default"; Excludes: ".*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Q:\app\Au\_\Templates\files\*"; DestDir: "{app}\Templates\files"; Flags: ignoreversion recursesubdirs
Source: "Q:\app\Au\_\Templates\files.xml"; DestDir: "{app}\Templates"; Flags: ignoreversion
Source: "Q:\app\Au\_\Cookbook\files\*"; DestDir: "{app}\Cookbook\files"; Flags: ignoreversion recursesubdirs
Source: "Q:\app\Au\_\Cookbook\files.xml"; DestDir: "{app}\Cookbook"; Flags: ignoreversion

Source: "Q:\app\Au\_\default.exe.manifest"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\Other\Data\doc.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\Other\Data\ref.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\Other\Data\winapi.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\icons.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "Q:\app\Au\_\xrefmap.yml"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
;Type: files; Name: "{app}\file.ext"
;Type: filesandordirs; Name: "{app}\dir"
Type: filesandordirs; Name: "{commonprograms}\Automaticode C#"
Type: filesandordirs; Name: "{commonprograms}\Autepad C#"
Type: filesandordirs; Name: "{commonprograms}\Derobotize Me C#"
;never mind: can't delete {pf}\Autepad etc. Users may not want it.

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
;register app path
Root: HKLM; Subkey: Software\Microsoft\Windows\CurrentVersion\App Paths\Au.Editor.exe; ValueType: string; ValueData: {app}\Au.Editor.exe; Flags: uninsdeletevalue uninsdeletekeyifempty

[Tasks]
Name: "NetDownload"; Description: "Open the .NET download webpage"; Check: NeedDotnet
;note: don't use the automatic .NET Runtime installation script: https://www.codeproject.com/Articles/20868/Inno-Setup-Dependency-Installer
;  It is too fragile etc. The URL may vanish. Always uses a hardcoded version (need to edit everytime), even if a compatible version is installed. No SDK. Once the downloaded .NET setup program crashed. Entire setup fails if the script fails, eg because of .NET connection.

[Run]
Filename: "https://dotnet.microsoft.com/en-us/download/dotnet/6.0"; Flags: nowait shellexec skipifsilent; Tasks: NetDownload
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent; Description: "{cm:LaunchProgram,{#MyAppName}}"

[Code]
var _needNet: Boolean;

procedure Cpp_Install(step: Integer; dir: String);
external 'Cpp_Install@files:Setup32.dll cdecl setuponly delayload';

procedure Cpp_Uninstall(step: Integer);
external 'Cpp_Uninstall@{app}\Setup32.dll cdecl uninstallonly delayload';

function Cpp_NeedDotnet(): Boolean;
external 'Cpp_NeedDotnet@files:Setup32.dll cdecl setuponly delayload';

function NeedDotnet(): Boolean;
begin
	Result := Cpp_NeedDotnet(); //can't call directly from Tasks
	_needNet := Result;
end;

function InitializeSetup(): Boolean;
begin
  Cpp_Install(0, '');
  Result:=true;
end;

function InitializeUninstall(): Boolean;
begin
  Cpp_Uninstall(0);
  Result:=true;
end;

procedure CurStepChanged(CurStep: TSetupStep);
//var
//  s1: String;
begin
//  s1:=Format('%d', [CurStep]);
//  MsgBox(s1, mbInformation, MB_OK);
  
  case CurStep of
    ssInstall:
    begin
      //Cpp_Install(1, ExpandConstant('{app}\'));
    end;
    ssPostInstall:
    begin
      Cpp_Install(2, ExpandConstant('{app}\'));
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  s1: String;
begin
//  s1:=Format('%d', [CurUninstallStep]);
//  MsgBox(s1, mbInformation, MB_OK);
  
  case CurUninstallStep of
    usUninstall:
    begin
      Cpp_Uninstall(1);
      UnloadDLL(ExpandConstant('{app}\Setup32.dll'));
    end;
    usPostUninstall:
    begin
      s1:=ExpandConstant('{app}');
      if DirExists(s1) and not RemoveDir(s1) then begin RestartReplace(s1, ''); end;
    end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var page: TWizardPage;
var txt: TNewStaticText;
begin
  //MsgBox(Format('%d', [CurPageID]), mbInformation, MB_OK);
  
  case CurPageID of
    wpSelectTasks:
    begin
			if _needNet then begin
				//replace the default Tasks page static text. It's better than using [Tasks] GroupDescription for it.
				page := PageFromID(CurPageID);
				txt := TNewStaticText(page.Surface.Controls[1]); //Controls[0] is the checkgroup
				txt.AutoSize := True;
				txt.Caption := 'You will need to download and install .NET 6 SDK x64 (~200 MB). Or .NET 6 Desktop Runtime x64 (~60 MB) now and SDK later (when/if will need).';
			end;
    end;
  end;
end;

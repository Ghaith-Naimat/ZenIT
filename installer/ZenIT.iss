#define MyAppName "ZenIT"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ZenHR IT"
#define MyAppExeName "ZenIT.exe"
#define SourceRoot "C:\ZenIT"

[Setup]
AppId={{E4DA53E2-12C9-43D7-A79D-9D1ADBB2F7B6}
AppName=ZenIT
AppVersion=1.0.0
AppPublisher=ZenHR IT
AppVerName=ZenIT 1.0.0
DefaultDirName={autopf}\ZenIT
DefaultGroupName=ZenIT
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputDir={#SourceRoot}\publish\installer
OutputBaseFilename=ZenIT-Setup
Compression=lzma2
SolidCompression=yes
SetupIconFile={#SourceRoot}\src\ZenIT.App\Assets\ZenIT.ico
UninstallDisplayName=ZenIT
UninstallDisplayIcon={app}\ZenIT.exe
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceRoot}\publish\jumpcloud\ZenIT.exe"; DestDir: "{app}"; DestName: "ZenIT.exe"; Flags: ignoreversion
Source: "{#SourceRoot}\installer\Configure-ZenIT.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Dirs]
Name: "{commonappdata}\ZenIT\Config"
Name: "{commonappdata}\ZenIT\Policy"
Name: "{commonappdata}\ZenIT\Logs"
Name: "{commonappdata}\ZenIT\Reports"

[Icons]
Name: "{commondesktop}\ZenIT"; Filename: "{app}\ZenIT.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ZenIT.exe"; IconIndex: 0
Name: "{commonprograms}\ZenIT"; Filename: "{app}\ZenIT.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ZenIT.exe"; IconIndex: 0

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{tmp}\Configure-ZenIT.ps1"""; StatusMsg: "Configuring ZenIT..."; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files; Name: "{commondesktop}\ZenIT.lnk"
Type: files; Name: "{commonprograms}\ZenIT.lnk"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -Command "Get-Process ZenIT -ErrorAction SilentlyContinue | ForEach-Object { try { if ($_.MainWindowHandle -ne 0) { [void]$_.CloseMainWindow(); Start-Sleep -Seconds 2 }; if (-not $_.HasExited) { Stop-Process -Id $_.Id -Force } } catch { } }"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Result := '';
end;

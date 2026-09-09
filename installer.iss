#define MyAppName "Bagua Live Wallpaper"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Bagua Live Wallpaper"
#define MyAppExeName "BaguaLiveWallpaper.exe"
#define MyAppScrName "BaguaLiveWallpaper.scr"

[Setup]
AppId={{A6F7D8D1-9C8D-4D35-9E72-7B2B9E8E6A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\BaguaLiveWallpaper
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
OutputBaseFilename=BaguaLiveWallpaper-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\{#MyAppScrName}"; DestDir: "{sys}"; Flags: ignoreversion restartreplace
Source: "publish\README.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{sys}\{#MyAppScrName}"; Flags: uninsdeletevalue

[Icons]
Name: "{autodesktop}\Bagua Live Wallpaper"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动八卦动态桌面"; Flags: postinstall nowait skipifsilent
Filename: "rundll32.exe"; Parameters: "shell32.dll,Control_RunDLL desk.cpl,,1"; Description: "打开 Windows 屏幕保护程序设置"; Flags: postinstall nowait skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

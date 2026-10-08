[Setup]
AppName=OptiPulse
AppVersion=1.0.5
DefaultDirName={autopf}\OptiPulse
DefaultGroupName=OptiPulse
OutputDir=bin\Release
OutputBaseFilename=OptiPulse_Setup
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\OptiPulse.exe
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

; Both shortcuts are created unconditionally: the desktop icon must not be
; an optional checkbox most users skip, and the Start Menu entry has always
; been mandatory. (The shortcuts are additionally deleted and recreated
; fresh by the [Code] section below.)
[Icons]
Name: "{group}\OptiPulse"; Filename: "{app}\OptiPulse.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\OptiPulse"; Filename: "{app}\OptiPulse.exe"; IconFilename: "{app}\icon.ico"

; Post-install: refresh Windows' icon cache and restart Explorer. When a new
; version installs over an existing installation at the same path (exactly
; what the app's auto-update does), the desktop shortcut would otherwise
; keep showing the old cached icon until Explorer is manually restarted.
; ie4uinit.exe is the legacy cache refresh tool: recent Windows 11 builds
; removed it entirely and the 32-bit installer's {sys} points at SysWOW64,
; so both entries skip silently where the file is absent - the Explorer
; restart below is what reliably refreshes icons on every system.
; runhidden keeps every step silent; runasoriginaluser relaunches Explorer
; as the normal (non-elevated) shell user. ie4uinit never blocks the setup.
[Run]
Filename: "{win}\Sysnative\ie4uinit.exe"; Parameters: "-show"; Flags: runhidden nowait skipifdoesntexist
Filename: "{sys}\ie4uinit.exe"; Parameters: "-show"; Flags: runhidden nowait skipifdoesntexist
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im explorer.exe"; Flags: runhidden waituntilterminated
Filename: "{win}\explorer.exe"; Flags: runhidden nowait runasoriginaluser

; Delete-then-recreate both shortcuts AFTER the files (and the [Icons]
; entries) are in place: an in-place .lnk overwrite does not reliably
; invalidate Windows' icon cache, but a fresh shortcut does.
[Code]
procedure RecreateShortcut(const LinkPath, Target, IconPath: string);
begin
  if FileExists(LinkPath) then
    DeleteFile(LinkPath);
  CreateShellLink(LinkPath, 'OptiPulse', Target, '',
    ExtractFilePath(Target), IconPath, 0, SW_SHOWNORMAL);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RecreateShortcut(ExpandConstant('{autodesktop}\OptiPulse.lnk'),
      ExpandConstant('{app}\OptiPulse.exe'), ExpandConstant('{app}\icon.ico'));
    RecreateShortcut(ExpandConstant('{group}\OptiPulse.lnk'),
      ExpandConstant('{app}\OptiPulse.exe'), ExpandConstant('{app}\icon.ico'));
  end;
end;

#define MyAppName "WtAgent"
#define MyAppPublisher "11ArkaN"
#define MyAppURL "https://github.com/11ArkaN/WtAgent"
#define MyAppExeName "WtAgent.exe"
#define MyAppCmdName "wt-agent.cmd"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-local"
#endif
#ifndef AppSourceDir
  #error AppSourceDir is required
#endif
#ifndef SkillSourceDir
  #error SkillSourceDir is required
#endif

[Setup]
AppId={{5A766DC8-B84D-467D-89EF-3A3C5C28A7B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\WtAgent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=WtAgent-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesEnvironment=yes

[Tasks]
Name: "addtopath"; Description: "Add wt-agent to the user PATH"; Flags: checkedonce
Name: "skill_codex"; Description: "Install wt-agent-terminal skill for Codex"; Flags: checkedonce
Name: "skill_claude"; Description: "Install wt-agent-terminal skill for Claude Code"
Name: "skill_cursor"; Description: "Install wt-agent-terminal skill for Cursor"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\wt-agent.cmd"; DestDir: "{app}"; DestName: "{#MyAppCmdName}"; Flags: ignoreversion
Source: "{#SkillSourceDir}\*"; DestDir: "{code:GetCodexSkillDir}"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: skill_codex
Source: "{#SkillSourceDir}\*"; DestDir: "{code:GetClaudeSkillDir}"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: skill_claude
Source: "{#SkillSourceDir}\*"; DestDir: "{code:GetCursorSkillDir}"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: skill_cursor

[Icons]
Name: "{autoprograms}\WtAgent"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\Uninstall WtAgent"; Filename: "{uninstallexe}"

[Code]
const
  EnvironmentKey = 'Environment';
  EnvironmentValue = 'Path';

function RemoveTrailingBackslash(const Value: string): string;
begin
  Result := Value;
  while (Length(Result) > 0) and (Result[Length(Result)] = '\') do
  begin
    Delete(Result, Length(Result), 1);
  end;
end;

function AppendPath(const OriginalPath, DirectoryToAdd: string): string;
var
  NormalizedOriginal: string;
  NormalizedToAdd: string;
begin
  NormalizedOriginal := ';' + Lowercase(RemoveTrailingBackslash(OriginalPath)) + ';';
  NormalizedToAdd := ';' + Lowercase(RemoveTrailingBackslash(DirectoryToAdd)) + ';';

  if Pos(NormalizedToAdd, NormalizedOriginal) > 0 then
  begin
    Result := OriginalPath;
  end
  else if OriginalPath = '' then
  begin
    Result := DirectoryToAdd;
  end
  else
  begin
    Result := OriginalPath + ';' + DirectoryToAdd;
  end;
end;

function RemovePathEntry(const OriginalPath, DirectoryToRemove: string): string;
var
  Candidate: string;
  SeparatorPosition: Integer;
  Remaining: string;
begin
  Result := '';

  Remaining := OriginalPath;
  while Remaining <> '' do
  begin
    SeparatorPosition := Pos(';', Remaining);
    if SeparatorPosition > 0 then
    begin
      Candidate := Trim(Copy(Remaining, 1, SeparatorPosition - 1));
      Delete(Remaining, 1, SeparatorPosition);
    end
    else
    begin
      Candidate := Trim(Remaining);
      Remaining := '';
    end;

    if Candidate = '' then
      continue;

    if Lowercase(RemoveTrailingBackslash(Candidate)) = Lowercase(RemoveTrailingBackslash(DirectoryToRemove)) then
      continue;

    if Result = '' then
      Result := Candidate
    else
      Result := Result + ';' + Candidate;
  end;
end;

procedure UpdateUserPath(const DirectoryToManage: string; AddEntry: Boolean);
var
  ExistingPath: string;
  UpdatedPath: string;
begin
  if not RegQueryStringValue(HKCU, EnvironmentKey, EnvironmentValue, ExistingPath) then
    ExistingPath := '';

  if AddEntry then
    UpdatedPath := AppendPath(ExistingPath, DirectoryToManage)
  else
    UpdatedPath := RemovePathEntry(ExistingPath, DirectoryToManage);

  if ExistingPath <> UpdatedPath then
  begin
    RegWriteStringValue(HKCU, EnvironmentKey, EnvironmentValue, UpdatedPath);
  end;
end;

function GetUserProfileDir: string;
begin
  Result := GetEnv('USERPROFILE');
  if Result = '' then
    Result := ExpandConstant('{localappdata}\..\..');
end;

function GetCodexSkillDir(Param: string): string;
var
  CodexHome: string;
begin
  CodexHome := GetEnv('CODEX_HOME');
  if CodexHome = '' then
    CodexHome := AddBackslash(GetUserProfileDir) + '.codex';
  Result := AddBackslash(CodexHome) + 'skills\wt-agent-terminal';
end;

function GetClaudeSkillDir(Param: string): string;
begin
  Result := AddBackslash(GetUserProfileDir) + '.claude\skills\wt-agent-terminal';
end;

function GetCursorSkillDir(Param: string): string;
begin
  Result := AddBackslash(GetUserProfileDir) + '.cursor\skills\wt-agent-terminal';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
  begin
    UpdateUserPath(ExpandConstant('{app}'), True);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    UpdateUserPath(ExpandConstant('{app}'), False);
  end;
end;

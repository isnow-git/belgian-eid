; =============================================================================
;  Belgian eID — Inno Setup 6
;
;  DO NOT compile directly: run installer\build.ps1 which generates
;  the artefacts and installer-config.iss before calling iscc.
; =============================================================================

#include "artifacts\installer-config.iss"

#define AppName     "Belgian eID"
#define AppPublisher "Simon Villers"
#define NmHostName  "be.belgianeid.bridge"
#define NmHostDesc  "Belgian eID Bridge"

; ── Metadata ──────────────────────────────────────────────────────────────────

[Setup]
AppId                     = {{6F4A2C1B-8D3E-4F5A-9B2C-1D0E3F4A5B6C}
AppName                   = {#AppName}
AppVersion                = {#AppVersion}
AppPublisher              = {#AppPublisher}
AppPublisherURL           = https://github.com/isnow-git/belgian-eid
AppSupportURL             = https://github.com/isnow-git/belgian-eid/issues
DefaultDirName            = {autopf}\BelgianEid
DefaultGroupName          = {#AppName}
DisableDirPage            = yes
DisableProgramGroupPage   = yes
OutputDir                 = output
OutputBaseFilename        = BelgianEidSetup-{#AppVersion}
Compression               = lzma2/ultra64
SolidCompression          = yes
PrivilegesRequired        = admin
ArchitecturesInstallIn64BitMode = x64compatible
MinVersion                = 10.0.17763
WizardStyle               = modern
WizardSizePercent         = 110
UninstallDisplayName      = {#AppName}
UninstallDisplayIcon      = {app}\bridge\BelgianEidBridge.exe
CloseApplications         = yes
RestartApplications       = no

; ── Languages ─────────────────────────────────────────────────────────────────

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Files ─────────────────────────────────────────────────────────────────────

[Files]
; Bridge — all published binaries (self-contained)
Source: "artifacts\bridge\*"; \
    DestDir: "{app}\bridge"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Packaged Chrome extension
Source: "artifacts\extension\BelgianEid.crx"; \
    DestDir: "{app}\extension"; \
    Flags: ignoreversion

; ── Registry ──────────────────────────────────────────────────────────────────

[Registry]
; Native Messaging host — 64-bit view (managed automatically by ArchitecturesInstallIn64BitMode)
Root: HKLM; \
    Subkey: "SOFTWARE\Google\Chrome\NativeMessagingHosts\{#NmHostName}"; \
    ValueType: string; ValueName: ""; \
    ValueData: "{app}\bridge\{#NmHostName}.json"; \
    Flags: uninsdeletekey

; Chrome Group Policy — force-install extension (64-bit view)
Root: HKLM; \
    Subkey: "SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist"; \
    ValueType: string; ValueName: "1"; \
    ValueData: "{code:GetExtensionPolicyValue}"; \
    Flags: uninsdeletevalue

; ── Pascal code ───────────────────────────────────────────────────────────────

[Code]

const
  ExtensionId = '{#ExtensionId}';
  AppVersion  = '{#AppVersion}';
  NmHostName  = '{#NmHostName}';

procedure ReplaceChar(var S: string; OldChar, NewChar: char);
var i: integer;
begin
  for i := 1 to Length(S) do
    if S[i] = OldChar then S[i] := NewChar;
end;

function UrlEncodePath(S: string): string;
var i: integer; R: string;
begin
  R := '';
  for i := 1 to Length(S) do
    if S[i] = ' ' then R := R + '%20'
    else R := R + S[i];
  Result := R;
end;

// Callback for [Registry] — builds the ExtensionInstallForcelist value.
function GetExtensionPolicyValue(Param: string): string;
var XmlPath: string;
begin
  XmlPath := ExpandConstant('{app}') + '\extension\update.xml';
  ReplaceChar(XmlPath, '\', '/');
  Result := ExtensionId + ';file:///' + UrlEncodePath(XmlPath);
end;

procedure WriteNativeHostManifest(AppDir: string);
var
  Path, ExePath, Json: string;
begin
  ExePath := AppDir + '\bridge\BelgianEidBridge.exe';
  ReplaceChar(ExePath, '\', '/');

  Json :=
    '{' + #13#10 +
    '  "name": "' + NmHostName + '",' + #13#10 +
    '  "description": "{#NmHostDesc}",' + #13#10 +
    '  "path": "' + ExePath + '",' + #13#10 +
    '  "type": "stdio",' + #13#10 +
    '  "allowed_origins": [' + #13#10 +
    '    "chrome-extension://' + ExtensionId + '/"' + #13#10 +
    '  ]' + #13#10 +
    '}';

  Path := AppDir + '\bridge\' + NmHostName + '.json';
  SaveStringToFile(Path, Json, False);
end;

procedure WriteUpdateManifest(AppDir: string);
var
  CrxPath, XmlPath, Xml: string;
begin
  CrxPath := AppDir + '\extension\BelgianEid.crx';
  ReplaceChar(CrxPath, '\', '/');

  Xml :=
    '<?xml version="1.0" encoding="UTF-8"?>' + #13#10 +
    '<gupdate xmlns="http://www.google.com/update2/response" protocol="2.0">' + #13#10 +
    '  <app appid="' + ExtensionId + '">' + #13#10 +
    '    <updatecheck' + #13#10 +
    '      codebase="file:///' + UrlEncodePath(CrxPath) + '"' + #13#10 +
    '      version="' + AppVersion + '" />' + #13#10 +
    '  </app>' + #13#10 +
    '</gupdate>';

  XmlPath := AppDir + '\extension\update.xml';
  SaveStringToFile(XmlPath, Xml, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var AppDir: string;
begin
  if CurStep = ssPostInstall then
  begin
    AppDir := ExpandConstant('{app}');
    WriteNativeHostManifest(AppDir);
    WriteUpdateManifest(AppDir);
  end;
end;

// ── Custom finish page ────────────────────────────────────────────────────────

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
    MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: string): string;
begin
  Result :=
    'eID Bridge:' + NewLine + Space +
    ExpandConstant('{app}\bridge\BelgianEidBridge.exe') + NewLine + NewLine +
    'Chrome Extension:' + NewLine + Space +
    'Automatic installation via Chrome policy (Group Policy)' + NewLine + Space +
    'ID: ' + ExtensionId + NewLine + NewLine +
    'Restart Google Chrome after installation to activate the extension.';
end;

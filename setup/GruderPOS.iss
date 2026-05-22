#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName      "POS Gruder"
#define AppId        "GruderPOS"
#define AppPublisher "Trigenius"
#define AppExeName   "GruderPOS.exe"
#define PublishDir   "..\GruderPOS\bin\Release\net8.0-windows\win-x64\publish"
#define OutputDir    "..\installers"

[Setup]
AppId={{A3C5E7F9-1B2D-4E6F-8A0C-2B4D6E8F0A2C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppId}
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename={#AppId}-Setup-{#AppVersion}
SetupIconFile={#PublishDir}\wwwroot\assets\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho no Ambiente de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

// WebView2 Runtime: verifica instalacao global ou por utilizador
function IsWebView2RuntimeInstalled(): Boolean;
const
  WebView2Guid = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2Guid, 'pv', Version) or
    RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2Guid, 'pv', Version) or
    RegQueryStringValue(HKCU,   'Software\Microsoft\EdgeUpdate\Clients\' + WebView2Guid, 'pv', Version);
  if Result then
    Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  Missing: String;
begin
  Result := True;
  Missing := '';

  if not IsDotNet8DesktopRuntimeInstalled() then
    Missing := Missing + '  - .NET 8 Desktop Runtime (x64)' + #13#10;

  if not IsWebView2RuntimeInstalled() then
    Missing := Missing + '  - Microsoft WebView2 Runtime' + #13#10;

  if Missing <> '' then
  begin
    if MsgBox(
      'Os seguintes componentes necessarios nao foram encontrados:' + #13#10 + #13#10 +
      Missing + #13#10 +
      'Deseja abrir a pagina de downloads?',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not IsDotNet8DesktopRuntimeInstalled() then
        ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ErrorCode);
      if not IsWebView2RuntimeInstalled() then
        ShellExec('open', 'https://developer.microsoft.com/microsoft-edge/webview2/', '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

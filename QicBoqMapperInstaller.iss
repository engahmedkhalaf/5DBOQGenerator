; ==========================================================
; QIC 5D BOQ Manager - Installer
; Inno Setup Script
; ==========================================================

#define MyAppName "QIC 5D BOQ Manager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "QIC"
#define MyAppURL "https://qic.com"
#define MyAppExeName "QicBoqMapper.dll"
#define PublishDir "D:\API Khalaf\Revit API\Publish\5D api"

[Setup]
AppId={{A1F3E8B4-1234-5678-ABCD-987654321000}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={commonappdata}\Autodesk\Revit\Addins\2023
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=QIC_5D_BOQ_Manager_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\QicBoqMapper.addin"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\QicBoqMapper\QicBoqMapper.dll"; DestDir: "{app}\QicBoqMapper"; Flags: ignoreversion
Source: "{#PublishDir}\QicBoqMapper\EPPlus.dll"; DestDir: "{app}\QicBoqMapper"; Flags: ignoreversion
Source: "{#PublishDir}\QicBoqMapper\Microsoft.IO.RecyclableMemoryStream.dll"; DestDir: "{app}\QicBoqMapper"; Flags: ignoreversion
Source: "{#PublishDir}\QicBoqMapper\System.ComponentModel.Annotations.dll"; DestDir: "{app}\QicBoqMapper"; Flags: ignoreversion
Source: "{#PublishDir}\QicBoqMapper\Resources\*"; DestDir: "{app}\QicBoqMapper\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Revit Add-ins typically do not require Start Menu shortcuts

[Run]
; Optional post-install actions

[Code]

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

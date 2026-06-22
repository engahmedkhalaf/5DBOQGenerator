; ==========================================================
; QIC 5D BOQ Manager - Installer
; Revit 2023
; ==========================================================

#define MyAppName "QIC 5D BOQ Manager"
#define MyAppVersion "1.0.0"
#define PublishDir "C:\ProgramData\Autodesk\Revit\Addins\2023"

[Setup]
AppId={{A1F3E8B4-1234-5678-ABCD-987654321000}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=QIC_5D_BOQ_Manager_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper"

[Files]
; Add-in manifest
Source: "QicBoqMapper\QicBoqMapper.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion

; All application files (DLL, PDB, Resources subfolder)
Source: "QicBoqMapper\bin\Debug\net48\*"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

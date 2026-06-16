; Inno Setup Script for QIC 5D BOQ Manager Revit 2023 Add-in
; Use Inno Setup Compiler to compile this script into a standalone installer executable.

[Setup]
AppName=QIC 5D BOQ Manager
AppVersion=1.0.0
AppPublisher=QicTools
AppPublisherURL=https://github.com/engahmedkhalaf/5DBOQGenerator
DefaultDirName={commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper
DefaultGroupName=QIC 5D BOQ Manager
DisableProgramGroupPage=yes
OutputBaseFilename=QicBoqMapperSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=QicBoqMapper\Resources\QicBoqManager_32.png
PrivilegesRequired=admin

[Dirs]
Name: "{commonappdata}\Autodesk\Revit\Addins\2023"
Name: "{commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper"
Name: "{commonappdata}\Autodesk\Revit\Addins\2023\QicBoqMapper\Resources"

[Files]
; Revit Add-in Manifest File (installed to the parent Addins\2023 folder)
Source: "QicBoqMapper\QicBoqMapper.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion

; Add-in Assembly and Dependencies
Source: "QicBoqMapper\bin\Debug\net48\QicBoqMapper.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "QicBoqMapper\bin\Debug\net48\EPPlus.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "QicBoqMapper\bin\Debug\net48\Microsoft.IO.RecyclableMemoryStream.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "QicBoqMapper\bin\Debug\net48\System.ComponentModel.Annotations.dll"; DestDir: "{app}"; Flags: ignoreversion

; Resources and Icons
Source: "QicBoqMapper\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Uninstall QIC 5D BOQ Manager"; Filename: "{uninstallexe}"

[Messages]
WelcomeLabel2=This wizard will install QIC 5D BOQ Manager Revit 2023 Add-in on your computer.%n%nPlease close Autodesk Revit before continuing.

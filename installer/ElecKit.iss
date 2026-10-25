#define ProductName "ElecKit"
#define ProductVersion "1.0.0"
#define Publisher "ElecKit"

[Setup]
AppId={{FEEA81A1-1B6A-4E6B-88AA-3BCE47F2A220}
AppName={#ProductName} for Autodesk Revit
AppVersion={#ProductVersion}
AppPublisher={#Publisher}
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\Release
OutputBaseFilename=ElecKit-Installer
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
Uninstallable=yes
UninstallDisplayName=ElecKit for Autodesk Revit

[Types]
Name: "full"; Description: "Install for all supported Revit versions"
Name: "custom"; Description: "Choose Revit versions"; Flags: iscustom

[Components]
Name: "rv2020"; Description: "Revit 2020"; Types: full custom
Name: "rv2021"; Description: "Revit 2021"; Types: full custom
Name: "rv2022"; Description: "Revit 2022"; Types: full custom
Name: "rv2023"; Description: "Revit 2023"; Types: full custom
Name: "rv2024"; Description: "Revit 2024"; Types: full custom
Name: "rv2025"; Description: "Revit 2025"; Types: full custom
Name: "rv2026"; Description: "Revit 2026"; Types: full custom
Name: "rv2027"; Description: "Revit 2027"; Types: full custom

[Files]
Source: "..\Release\RevitVersions\2020\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2020"; Components: rv2020; Flags: ignoreversion
Source: "..\Release\RevitVersions\2020\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2020"; Components: rv2020; Flags: ignoreversion
Source: "..\Release\RevitVersions\2021\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Components: rv2021; Flags: ignoreversion
Source: "..\Release\RevitVersions\2021\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Components: rv2021; Flags: ignoreversion
Source: "..\Release\RevitVersions\2022\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Components: rv2022; Flags: ignoreversion
Source: "..\Release\RevitVersions\2022\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Components: rv2022; Flags: ignoreversion
Source: "..\Release\RevitVersions\2023\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Components: rv2023; Flags: ignoreversion
Source: "..\Release\RevitVersions\2023\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Components: rv2023; Flags: ignoreversion
Source: "..\Release\RevitVersions\2024\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Components: rv2024; Flags: ignoreversion
Source: "..\Release\RevitVersions\2024\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Components: rv2024; Flags: ignoreversion
Source: "..\Release\RevitVersions\2025\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Components: rv2025; Flags: ignoreversion
Source: "..\Release\RevitVersions\2025\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Components: rv2025; Flags: ignoreversion
Source: "..\Release\RevitVersions\2026\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Components: rv2026; Flags: ignoreversion
Source: "..\Release\RevitVersions\2026\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Components: rv2026; Flags: ignoreversion
Source: "..\Release\RevitVersions\2027\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Components: rv2027; Flags: ignoreversion
Source: "..\Release\RevitVersions\2027\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Components: rv2027; Flags: ignoreversion

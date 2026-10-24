#define AppName "ElecKit"
#define AppVersion "1.0.0"
#define Publisher "ElecKit"
#define RepoUrl "https://github.com/amdghedy/BimTools.ElecKit"

[Setup]
AppId={{A8A4C0E1-0D0B-4B1E-9F2E-EEA2D65B9B21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL={#RepoUrl}
DefaultDirName={userappdata}\Autodesk\Revit
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=ElecKit-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
Uninstallable=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "revit2020"; Description: "Install for Revit 2020"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2021"; Description: "Install for Revit 2021"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2022"; Description: "Install for Revit 2022"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2023"; Description: "Install for Revit 2023"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2024"; Description: "Install for Revit 2024"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2025"; Description: "Install for Revit 2025"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked
Name: "revit2026"; Description: "Install for Revit 2026"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked

Name: "revit2027"; Description: "Install for Revit 2027"; GroupDescription: "Select Revit versions to install:"; Flags: unchecked

[Files]
Source: "..\artifacts\Revit2020\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2020\ElecKit"; Flags: ignoreversion; Tasks: revit2020
Source: "..\artifacts\Revit2020\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2020"; Flags: ignoreversion; Tasks: revit2020
Source: "..\artifacts\Revit2021\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021\ElecKit"; Flags: ignoreversion; Tasks: revit2021
Source: "..\artifacts\Revit2021\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion; Tasks: revit2021
Source: "..\artifacts\Revit2022\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\ElecKit"; Flags: ignoreversion; Tasks: revit2022
Source: "..\artifacts\Revit2022\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion; Tasks: revit2022
Source: "..\artifacts\Revit2023\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\ElecKit"; Flags: ignoreversion; Tasks: revit2023
Source: "..\artifacts\Revit2023\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion; Tasks: revit2023
Source: "..\artifacts\Revit2024\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\ElecKit"; Flags: ignoreversion; Tasks: revit2024
Source: "..\artifacts\Revit2024\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion; Tasks: revit2024
Source: "..\artifacts\Revit2025\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\ElecKit"; Flags: ignoreversion; Tasks: revit2025
Source: "..\artifacts\Revit2025\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion; Tasks: revit2025
Source: "..\artifacts\Revit2026\ElecKit.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\ElecKit"; Flags: ignoreversion; Tasks: revit2026
Source: "..\artifacts\Revit2026\ElecKit.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Tasks: revit2026

Source: "..\\artifacts\\Revit2027\\ElecKit.dll"; DestDir: "{userappdata}\\Autodesk\\Revit\\Addins\\2027\\ElecKit"; Flags: ignoreversion; Tasks: revit2027
Source: "..\\artifacts\\Revit2027\\ElecKit.addin"; DestDir: "{userappdata}\\Autodesk\\Revit\\Addins\\2027"; Flags: ignoreversion; Tasks: revit2027

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsWin64;
  if not Result then
    MsgBox('ElecKit requires 64-bit Windows and a 64-bit Revit installation.', mbError, MB_OK);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectTasks then
  begin
    if not WizardIsTaskSelected('revit2020') and
       not WizardIsTaskSelected('revit2021') and
       not WizardIsTaskSelected('revit2022') and
       not WizardIsTaskSelected('revit2023') and
       not WizardIsTaskSelected('revit2024') and
       not WizardIsTaskSelected('revit2025') and
       not WizardIsTaskSelected('revit2026') and
       not WizardIsTaskSelected('revit2027') then
    begin
      MsgBox('Select at least one Revit version to install ElecKit.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

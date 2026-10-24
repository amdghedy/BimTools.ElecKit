# ElecKit

A Revit 2020 electrical add-in, written from scratch. Tools are listed in [ROADMAP.md](ROADMAP.md): 42 requested functions plus Circuit Report, Select by Circuit, Options and Open Log.

## Build
Local (Revit 2020 installed): open the folder in Visual Studio 2022 and build, or `dotnet build -c Debug`.
Debug builds copy the add-in to `%APPDATA%\Autodesk\Revit\Addins\2020`. Close Revit first.

CI builds with the Revit API NuGet packages (see `.github/workflows/build.yml`).

## Add a tool
1. Create `src/Commands/MyToolCommand.cs`, derive from `ElecCommand`, add `[Transaction(...)]`.
2. Add one entry to `src/Core/ToolRegistry.cs`. The ribbon button appears automatically.

Logs: `%LOCALAPPDATA%\ElecKit\log.txt` - Settings: `%APPDATA%\ElecKit\settings.json`

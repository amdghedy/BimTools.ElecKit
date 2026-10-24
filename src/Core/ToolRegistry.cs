using System;
using System.Collections.Generic;
using ElecKit.Commands;

namespace ElecKit.Core
{
    public sealed class ToolInfo
    {
        public string Id;
        public string Title;
        public string Panel;
        public string Tooltip;
        public Type Command;
    }

    /// <summary>The single list of tools. Add a line here and the ribbon button appears.</summary>
    public static class ToolRegistry
    {
        private static ToolInfo T(string id, string title, string panel, string tooltip, Type command)
        {
            return new ToolInfo { Id = id, Title = title, Panel = panel, Tooltip = tooltip, Command = command };
        }

        public static readonly IReadOnlyList<ToolInfo> Tools = new List<ToolInfo>
        {
            // Drafting
            T("CadToDetail", "CAD to\nDetail Lines", "Drafting",
              "Redraws the lines and arcs of an imported or linked CAD file as detail lines in the active view. Optional layer filter.",
              typeof(CadToDetailCommand)),
            T("WiresToDetail", "Wires to\nDetail Lines", "Drafting",
              "Redraws electrical wires as detail lines in the active view. Uses the selected wires, or every wire in the view.",
              typeof(WiresToDetailCommand)),
            T("DetailToWire", "Detail Lines\nto Wires", "Drafting",
              "Turns connected straight detail lines into chamfered electrical wires, one wire per chain of lines.",
              typeof(DetailToWireCommand)),
            T("ConnectRuns", "Connect\nRuns", "Drafting",
              "Pick elements in order and connect each pair with a conduit run (direct or L-shaped with an elbow) at a set height.",
              typeof(ConnectRunsCommand)),
            T("LinesToConduit", "Lines to\nConduit", "Drafting",
              "Converts the selected straight detail or model lines into conduits. Asks for conduit type and height above the level.",
              typeof(LinesToConduitCommand)),
            T("FilletRadius", "Fillet\nRadius", "Drafting",
              "Sets the default fillet radius (mm) used by the fillet tools.",
              typeof(FilletRadiusCommand)),
            T("FilletTwoLines", "Fillet\nTwo Lines", "Drafting",
              "Pick two detail lines (click the part of each to keep) and round the corner. Repeats until you press Esc.",
              typeof(FilletTwoLinesCommand)),
            T("FilletCorners", "Fillet\nCorners", "Drafting",
              "Rounds every corner where two straight detail lines meet. Uses the selection, or the whole view if nothing is selected.",
              typeof(FilletCornersCommand)),
            T("SplitLines", "Split\nLines", "Drafting",
              "Splits straight detail lines wherever another line crosses or touches them. Uses the selection, or the whole view.",
              typeof(SplitLinesCommand)),
            T("CleanLines", "Clean\nLines", "Drafting",
              "Deletes duplicate or fully overlapping detail lines (same line style). Uses the selection, or the whole view if nothing is selected.",
              typeof(OverkillCommand)),
            // Wiring
            T("HomeRunLines", "Home Run\nLines", "Wiring",
              "Draws home-run detail lines from picked devices to a panel: one line per device, or a chain ending at the panel.",
              typeof(HomeRunLinesCommand)),
            T("BoundaryToConduits", "Boundary to\nConduits", "Wiring",
              "Turns a path or boundary of detail lines into one continuous conduit run with elbows at the corners.",
              typeof(BoundaryToConduitsCommand)),
            T("CircuitNumberText", "Circuit\nNumber Text", "Wiring",
              "Writes circuit number text into a parameter of the selected fixtures and equipment: same text, each element's own circuit, or a numbered sequence.",
              typeof(CircuitNumberTextCommand)),
            T("WireInfoToComments", "Wire Info to\nComments", "Wiring",
              "Writes the circuits and wire types of nearby devices and wires into each conduit's Comments.",
              typeof(WireInfoToCommentsCommand)),
            T("CableCatalog", "Cable\nCatalog", "Wiring",
              "Opens the cable catalog table for review and editing. Saved in your AppData folder.",
              typeof(CableCatalogCommand)),
            // Annotation
            T("SwitchLabels", "Switch\nLabels", "Annotation",
              "Writes switch labels (a, b, c / A, B, C / 1, 2, 3 or one fixed text) onto lighting devices in pick order.",
              typeof(SwitchLabelsCommand)),
            T("TagsWithLeaders", "Tags with\nLeaders", "Annotation",
              "Places a tag with a leader on each selected element, offset by a set distance on the sheet. Uses the default tag for each category.",
              typeof(TagsWithLeadersCommand)),
            T("AlignedDimension", "Aligned\nDimension", "Annotation",
              "Dimensions between two picked elements (family instances, grids or reference planes) along the direction that joins them.",
              typeof(AlignedDimensionCommand)),
            T("DistributeAlongPath", "Distribute\nAlong Path", "Annotation",
              "Spaces picked family instances evenly along a picked path (line, arc, conduit or tray).",
              typeof(DistributeAlongPathCommand)),
            // Model
            T("WorksetsByType", "Worksets\nby Type", "Model",
              "Moves selected elements into worksets named after their category, or family and type. Creates missing worksets.",
              typeof(WorksetsByTypeCommand)),
            T("SelectHosts", "Select\nHosts", "Model",
              "Selects the host elements of the currently selected family instances and zooms to them.",
              typeof(SelectHostsCommand)),
            T("PlaceJunctionBox", "Place\nJunction Box", "Model",
              "Places a junction box (level-based electrical fixture) at each point you click, at a set height. Repeats until Esc.",
              typeof(PlaceJunctionBoxCommand)),
            T("AlignToReference", "Align to\nReference", "Model",
              "Aligns picked family instances to a reference instance: rotation, location, or one axis.",
              typeof(AlignToReferenceCommand)),
            T("SectionAlongElement", "Section Along\nElement", "Model",
              "Creates a section view running along a picked straight conduit, tray or line.",
              typeof(SectionAlongElementCommand)),
            T("SelectSameType", "Select\nSame Type", "Model",
              "Pick an element, then select every element of the same type in the active view.",
              typeof(SelectSameTypeCommand)),
            T("ThreeDFromSelection", "3D from\nSelection", "Model",
              "Creates a 3D view with the section box fitted around the selected elements.",
              typeof(ThreeDFromSelectionCommand)),
            // Analysis
            T("ClashCheck", "Clash\nCheck", "Analysis",
              "Finds selected elements or a category that intersect a chosen category. Saves a CSV report and selects the elements involved.",
              typeof(ClashCheckCommand)),
            T("QuantityTakeoff", "Quantity\nTakeoff", "Analysis",
              "Counts selected elements (or the view) by category, family and type, with lengths for linear elements. Saves a CSV.",
              typeof(QuantityTakeoffCommand)),
            T("SmartFilter", "Smart\nFilter", "Analysis",
              "Finds elements in the view by category, type name and parameter value, then selects or isolates them.",
              typeof(SmartFilterCommand)),
            // Schedules
            T("ExportAllSchedules", "Export All\nSchedules", "Schedules",
              "Exports every schedule in the model to its own CSV file in a new folder.",
              typeof(ExportAllSchedulesCommand)),
            T("ExportActiveSchedule", "Export Active\nSchedule", "Schedules",
              "Exports the open schedule view to a single CSV file.",
              typeof(ExportActiveScheduleCommand)),
            T("ExportSchedulesExcel", "Schedules\nto Excel", "Schedules",
              "Exports the open schedule, or every schedule on its own sheet, to an Excel .xlsx file.",
              typeof(ExportSchedulesExcelCommand)),
            T("ImportValuesByMark", "Import by\nMark", "Schedules",
              "Reads a CSV and writes its values onto elements matched by Mark. Shows what will change before applying.",
              typeof(ImportValuesByMarkCommand)),
            T("CsvToTextNote", "CSV to\nText Note", "Schedules",
              "Places a CSV table in the view as one text note with aligned columns.",
              typeof(CsvToTextNoteCommand)),
            // Panels & SLD
            T("ViewManager", "View\nManager", "Panels & SLD",
              "Lists every printable view and sheet in a searchable list and opens the one you pick.",
              typeof(ViewManagerCommand)),
            T("SheetCreator", "Sheet\nCreator", "Panels & SLD",
              "Creates a new sheet and places the open view or schedule on it.",
              typeof(SheetCreatorCommand)),
            T("PanelSingleLine", "Panel\nSingle-Line", "Panels & SLD",
              "Builds a text single-line report for a picked panel: circuits, loads, poles, voltage. Saved as a text file.",
              typeof(PanelSingleLineCommand)),
            // Cable & Tray
            T("ConduitRunReport", "Conduit Run\nReport", "Cable & Tray",
              "Reports conduit type, size, length, level and comments for selected conduits (or the view), with totals by size.",
              typeof(ConduitRunReportCommand)),
            T("TraySummaryView", "Tray Summary\nView", "Cable & Tray",
              "Creates a drafting view with a summary of cable trays by type and size.",
              typeof(TraySummaryViewCommand)),
            T("DrawCableTray", "Draw Cable\nTray", "Cable & Tray",
              "Draws cable tray between clicked points at a set height and size, with elbows at the corners. Repeats until Esc.",
              typeof(DrawCableTrayCommand)),
            // Data
            T("SharedParameters", "Shared\nParameters", "Data",
              "Creates common electrical shared parameters and binds them to electrical categories.",
              typeof(SharedParametersCommand)),
            T("FamilyTypeManager", "Family Type\nManager", "Data",
              "Lists the types available for the selected element and switches all matching selected elements to the one you pick.",
              typeof(FamilyTypeManagerCommand)),
            // Circuits
            T("CircuitReport", "Circuit\nReport", "Circuits",
              "Exports every circuit (panel, circuit number, load name, element count) to a CSV file.",
              typeof(CircuitReportCommand)),
            T("SelectByCircuit", "Select by\nCircuit", "Circuits",
              "Pick one element and select every element on the same circuit(s).",
              typeof(SelectByCircuitCommand)),
            // Settings
            T("Options", "Options", "Settings",
              "Sets the report folder, whether reports open automatically, and the default fillet radius.",
              typeof(OptionsCommand)),
            T("OpenLog", "Open\nLog", "Settings",
              "Opens the ElecKit log file with the details of any error.",
              typeof(OpenLogCommand))
        };
    }
}

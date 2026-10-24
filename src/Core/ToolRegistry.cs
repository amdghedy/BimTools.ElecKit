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

            // Model
            T("SelectHosts", "Select\nHosts", "Model",
              "Selects the host elements of the currently selected family instances and zooms to them.",
              typeof(SelectHostsCommand)),
            T("SelectSameType", "Select\nSame Type", "Model",
              "Pick an element, then select every element of the same type in the active view.",
              typeof(SelectSameTypeCommand)),
            T("ThreeDFromSelection", "3D from\nSelection", "Model",
              "Creates a 3D view with the section box fitted around the selected elements.",
              typeof(ThreeDFromSelectionCommand)),

            // Schedules
            T("ExportAllSchedules", "Export All\nSchedules", "Schedules",
              "Exports every schedule in the model to its own CSV file in a new folder.",
              typeof(ExportAllSchedulesCommand)),
            T("ExportActiveSchedule", "Export Active\nSchedule", "Schedules",
              "Exports the open schedule view to a single CSV file.",
              typeof(ExportActiveScheduleCommand)),

            T("AssignWorkset", "Assign\nWorkset", "Model",
              "Assigns selected model instances to an existing user workset in a workshared document and reports elements that cannot be changed.",
              typeof(AssignWorksetCommand)),

            T("SmartFilter", "Smart\nFilter", "Model",
              "Pick an element and select matching instances by type, category, or exact parameter value in the active view; optionally temporarily isolate the matches.",
              typeof(SmartFilterCommand)),

            T("ClashDetection", "Clash\nReview", "Model",
              "Exports host/linked MEP overlap candidates to CSV. Choose MEP-only or MEP plus architectural/structural categories; verify exact geometry and clearances.",
              typeof(ClashDetectionCommand)),

            // Quantity reporting and data exchange
            T("QuantityTakeoff", "Quantity\nTakeoff", "Schedules",
              "Exports grouped quantities from the active view to CSV. Choose electrical categories only or include mechanical equipment, ductwork and pipework; curve lengths are in millimetres.",
              typeof(QuantityTakeoffCommand)),
            T("ImportScheduleValues", "Import CSV\nValues", "Schedules",
              "Imports writable parameter values from CSV using ElementId or unique Mark keys; previews the operation and reports skipped or rejected values.",
              typeof(ImportScheduleValuesCommand)),
            T("ExportElementParameters", "Export Element\nParameters", "Schedules",
              "Exports selected model instances to a keyed CSV with ElementId, Mark and common writable instance parameters for review and later import.",
              typeof(ExportElementParametersCommand)),

            // Circuits
            T("CircuitReport", "Circuit\nReport", "Circuits",
              "Exports every circuit (panel, circuit number, load name, element count) to a CSV file.",
              typeof(CircuitReportCommand)),
            T("SelectByCircuit", "Select by\nCircuit", "Circuits",
              "Pick one element and select every element on the same circuit(s).",
              typeof(SelectByCircuitCommand)),
            T("AssignCircuitPanel", "Assign Circuit\nPanel", "Circuits",
              "Pick a circuit element and destination electrical panel to reassign the circuit. Verify electrical compatibility and panel capacity afterward.",
              typeof(AssignCircuitPanelCommand))
        };
    }
}

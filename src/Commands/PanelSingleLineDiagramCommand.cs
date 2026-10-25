using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;
using Microsoft.Win32;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PanelSingleLineDiagramCommand : ElecCommand
    {
        private sealed class PanelFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is FamilyInstance && element.Category != null &&
                    element.Category.Id.Val() == (int)BuiltInCategory.OST_ElectricalEquipment;
            }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference reference;
            try
            {
                reference = ctx.UiDoc.Selection.PickObject(ObjectType.Element, new PanelFilter(),
                    "Pick the electrical panel for the single-line diagram");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            FamilyInstance panel = ctx.Doc.GetElement(reference) as FamilyInstance;
            if (panel == null) return Result.Cancelled;

            List<ElectricalSystem> systems = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ElectricalSystem)).Cast<ElectricalSystem>()
                .Where(s => { try { return s.BaseEquipment != null && s.BaseEquipment.Id == panel.Id; } catch { return false; } })
                .OrderBy(s => s.CircuitNumber).ThenBy(s => s.Name).ToList();

            string csvPath = null;
            var save = new SaveFileDialog
            {
                Title = "Export panel SLD data (Cancel to skip CSV)",
                Filter = "CSV (*.csv)|*.csv",
                FileName = "SLD_" + FileNames.Safe(panel.Name) + ".csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };
            if (save.ShowDialog() == true) csvPath = save.FileName;

            ViewDrafting drafting = null;
            string viewName = "SLD_" + FileNames.Safe(panel.Name) + "_" + DateTime.Now.ToString("HHmmss");
            int placed = 0;
            var rows = new List<string> { "Panel,Circuit,Load,Apparent Load,Connected Elements,Notes" };

            ctx.Transact("Create panel single-line diagram", () =>
            {
                ViewFamilyType draftingType = new FilteredElementCollector(ctx.Doc)
                    .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);
                if (draftingType == null)
                    throw new InvalidOperationException("No Drafting View type exists in this project.");
                drafting = ViewDrafting.Create(ctx.Doc, draftingType.Id);
                drafting.Name = viewName;

                TextNoteType noteType = new FilteredElementCollector(ctx.Doc)
                    .OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault();
                if (noteType == null)
                    throw new InvalidOperationException("No TextNoteType exists in this project.");

                double x0 = 0, y0 = 0, pitch = 1.25;
                double busLength = Math.Max(3.0, systems.Count * pitch + 1.0);
                ctx.Doc.Create.NewDetailCurve(drafting,
                    Line.CreateBound(new XYZ(x0, y0, 0), new XYZ(x0 + busLength, y0, 0)));
                TextNote.Create(ctx.Doc, drafting.Id, new XYZ(x0, y0 + 0.6, 0),
                    "PANEL: " + panel.Name + " | CIRCUITS: " + systems.Count, noteType.Id);

                string[] panelInfo = { "Voltage", "Mains Rating", "Main Rating", "Panel Rating", "Supply From" };
                var meta = new List<string>();
                foreach (string parameterName in panelInfo)
                {
                    Parameter p = panel.LookupParameter(parameterName) ?? (panel.Symbol == null ? null : panel.Symbol.LookupParameter(parameterName));
                    if (p == null) continue;
                    string value = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
                    if (!string.IsNullOrWhiteSpace(value)) meta.Add(parameterName + ": " + value);
                }
                if (meta.Count > 0)
                    TextNote.Create(ctx.Doc, drafting.Id, new XYZ(x0, y0 + 0.25, 0), string.Join(" | ", meta), noteType.Id);

                int index = 0;
                foreach (ElectricalSystem system in systems)
                {
                    double x = x0 + 0.5 + index * pitch;
                    string apparentLoad = "";
                    try
                    {
                        Parameter load = system.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
                        apparentLoad = load == null ? "" : (load.AsValueString() ?? load.AsString() ?? "");
                    }
                    catch { }
                    int count = 0;
                    try { count = system.Elements == null ? 0 : system.Elements.Size; } catch { }
                    string circuit = system.CircuitNumber ?? "";
                    string loadName = system.LoadName ?? system.Name ?? "";
                    ctx.Doc.Create.NewDetailCurve(drafting,
                        Line.CreateBound(new XYZ(x, y0, 0), new XYZ(x, y0 - 0.55, 0)));
                    ctx.Doc.Create.NewDetailCurve(drafting,
                        Line.CreateBound(new XYZ(x - 0.12, y0 - 0.28, 0), new XYZ(x + 0.12, y0 - 0.28, 0)));
                    TextNote.Create(ctx.Doc, drafting.Id, new XYZ(x - 0.45, y0 - 0.9, 0),
                        "Ckt " + circuit + "\n" + loadName + (string.IsNullOrWhiteSpace(apparentLoad) ? "" : "\n" + apparentLoad),
                        noteType.Id);
                    rows.Add(string.Join(",", new[] { panel.Name, circuit, loadName, apparentLoad, count.ToString(), "" }.Select(Csv.Escape)));
                    index++;
                    placed++;
                }
                if (systems.Count == 0)
                    TextNote.Create(ctx.Doc, drafting.Id, new XYZ(x0, y0 - 0.5, 0),
                        "No circuits are currently assigned to this panel.", noteType.Id);
            });

            if (drafting != null) ctx.UiDoc.ActiveView = drafting;
            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                File.WriteAllText(csvPath, string.Join(Environment.NewLine, rows) + Environment.NewLine, new UTF8Encoding(true));
                if (!ElecSettings.Current.DoNotOpenReports)
                    Process.Start(new ProcessStartInfo(csvPath) { UseShellExecute = true });
            }

            TaskDialog.Show("Panel Single-Line Diagram",
                "Created drafting view: " + viewName + "\nPanel: " + panel.Name +
                "\nCircuits drawn: " + placed +
                (string.IsNullOrWhiteSpace(csvPath) ? "" : "\nCSV: " + csvPath) +
                "\n\nThis is a schematic diagram, not an electrical design validation. Confirm panel ratings, voltage/phase, spare capacity and Revit panel schedule before issue.");
            return Result.Succeeded;
        }
    }
}

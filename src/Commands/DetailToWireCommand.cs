using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Turns connected straight detail lines into chamfered electrical wires.
    /// Each chain of lines that share end points becomes one wire; the wires are created unconnected.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DetailToWireCommand : ElecCommand
    {
        private const double JoinToleranceFeet = 0.003;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is View3D || view is ViewSchedule)
            {
                TaskDialog.Show("Detail Lines to Wires", "Open a plan or elevation view first.");
                return Result.Cancelled;
            }

            var sources = new List<DetailCurve>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var curve = ctx.Doc.GetElement(id) as DetailCurve;
                if (curve != null && curve.GeometryCurve is Line) sources.Add(curve);
            }
            if (sources.Count == 0)
            {
                var filter = new PredicateFilter(e =>
                {
                    var c = e as DetailCurve;
                    return c != null && c.GeometryCurve is Line;
                });
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                    "Select straight detail lines to convert, then click Finish"))
                {
                    var curve = ctx.Doc.GetElement(r) as DetailCurve;
                    if (curve != null) sources.Add(curve);
                }
            }
            if (sources.Count == 0) return Result.Cancelled;

            var types = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(WireType))
                .Cast<WireType>()
                .OrderBy(t => t.Name)
                .ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show("Detail Lines to Wires", "This project has no wire types. Load or create one first.");
                return Result.Cancelled;
            }

            int index = 0;
            if (types.Count > 1)
            {
                int last = types.FindIndex(t => t.Name == Settings.Current.LastWireType);
                index = Dialogs.Choose("Detail Lines to Wires", "Wire type:", types.Select(t => t.Name).ToList(), last);
                if (index < 0) return Result.Cancelled;
            }
            WireType wireType = types[index];
            Settings.Current.LastWireType = wireType.Name;
            Settings.Current.Save();

            var ask = new TaskDialog("Detail Lines to Wires");
            ask.MainInstruction = "Delete the source detail lines after converting?";
            ask.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
            ask.DefaultButton = TaskDialogResult.No;
            TaskDialogResult choice = ask.Show();
            if (choice == TaskDialogResult.Cancel) return Result.Cancelled;
            bool deleteSources = choice == TaskDialogResult.Yes;

            var lines = sources.Select(s => (Line)s.GeometryCurve).ToList();
            List<List<XYZ>> chains = LineChains.Build(lines, JoinToleranceFeet);

            int created = 0, closedLoops = 0, failed = 0;
            string firstError = null;

            ctx.Transact("Detail lines to wires", () =>
            {
                foreach (List<XYZ> chain in chains)
                {
                    if (chain.Count >= 3 && chain[0].DistanceTo(chain[chain.Count - 1]) <= JoinToleranceFeet)
                    {
                        closedLoops++;
                        continue;
                    }

                    List<XYZ> points = LineChains.RemoveStraightVertices(chain);
                    if (points.Count < 2) continue;

                    try
                    {
                        Wire.Create(ctx.Doc, wireType.Id, view.Id, WiringType.Chamfer, points, null, null);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        if (firstError == null) firstError = ex.Message;
                        failed++;
                    }
                }

                if (deleteSources && created > 0 && failed == 0)
                    ctx.Doc.Delete(sources.Select(s => s.Id).ToList());
            });

            string text = created + " wire(s) created (" + wireType.Name + ").";
            if (closedLoops > 0) text += "\n" + closedLoops + " closed loop(s) skipped: a wire needs two different ends.";
            if (failed > 0) text += "\n" + failed + " wire(s) failed: " + firstError;
            if (deleteSources && failed > 0) text += "\nSource lines were kept because some wires failed.";
            TaskDialog.Show("Detail Lines to Wires", text);
            return Result.Succeeded;
        }
    }
}

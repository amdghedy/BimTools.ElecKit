using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Redraws electrical wires as detail lines in the active view. The wires are not changed.
    /// Works on the selected wires, or every wire visible in the view if none are selected.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class WiresToDetailCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is View3D || view is ViewSchedule)
            {
                TaskDialog.Show("Wires to Detail Lines", "Open a plan, section or drafting view first.");
                return Result.Cancelled;
            }

            var selected = ctx.UiDoc.Selection.GetElementIds();
            List<Wire> wires;
            if (selected.Count > 0)
                wires = selected.Select(id => ctx.Doc.GetElement(id)).OfType<Wire>().ToList();
            else
                wires = new FilteredElementCollector(ctx.Doc, view.Id).OfClass(typeof(Wire)).Cast<Wire>().ToList();

            if (wires.Count == 0)
            {
                TaskDialog.Show("Wires to Detail Lines", "No wires were found in the selection or in this view.");
                return Result.Cancelled;
            }

            GraphicsStyle style;
            if (!LineStyles.TryChoose(ctx.Doc, "Wires to Detail Lines", out style)) return Result.Cancelled;

            var options = new Options();
            options.ComputeReferences = false;
            options.View = view;

            int created = 0, skippedWires = 0, failed = 0;
            ctx.Transact("Wires to detail lines", () =>
            {
                foreach (Wire wire in wires)
                {
                    var curves = new List<Curve>();
                    GeometryElement geometry = wire.get_Geometry(options);
                    if (geometry != null) GeometryCurves.Collect(geometry, o => true, curves);

                    // fall back to the wire's vertices if it exposes no curve geometry
                    if (curves.Count == 0)
                    {
                        for (int i = 0; i < wire.NumberOfVertices - 1; i++)
                        {
                            XYZ a = wire.GetVertex(i);
                            XYZ b = wire.GetVertex(i + 1);
                            if (a.DistanceTo(b) > FilletMath.ShortCurveFeet) curves.Add(Line.CreateBound(a, b));
                        }
                    }

                    bool any = false;
                    foreach (Curve curve in curves)
                    {
                        foreach (Curve piece in DetailCurves.Flatten(curve, view))
                        {
                            try
                            {
                                DetailCurves.Create(ctx.Doc, view, piece, style);
                                created++;
                                any = true;
                            }
                            catch (Exception ex)
                            {
                                if (failed == 0) Log.Error(ex);
                                failed++;
                            }
                        }
                    }
                    if (!any) skippedWires++;
                }
            });

            string text = created + " detail line(s) created from " + (wires.Count - skippedWires) + " wire(s).";
            if (skippedWires > 0) text += "\n" + skippedWires + " wire(s) produced no usable geometry in this view.";
            if (failed > 0) text += "\n" + failed + " curve(s) failed (see log).";
            TaskDialog.Show("Wires to Detail Lines", text);
            return Result.Succeeded;
        }
    }
}

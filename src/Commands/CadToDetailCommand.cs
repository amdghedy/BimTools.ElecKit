using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Redraws the lines and arcs of an imported or linked CAD file as Revit detail lines in the active view.
    /// The CAD import itself is left in place.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CadToDetailCommand : ElecCommand
    {
        private const int MaxCurves = 20000;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is View3D || view is ViewSchedule)
            {
                TaskDialog.Show("CAD to Detail Lines", "Open a plan, section or drafting view first.");
                return Result.Cancelled;
            }

            var imports = new List<ImportInstance>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var import = ctx.Doc.GetElement(id) as ImportInstance;
                if (import != null) imports.Add(import);
            }
            if (imports.Count == 0)
            {
                Reference picked = ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                    new PredicateFilter(e => e is ImportInstance), "Pick an imported or linked CAD file");
                var import = ctx.Doc.GetElement(picked) as ImportInstance;
                if (import != null) imports.Add(import);
            }
            if (imports.Count == 0) return Result.Cancelled;

            string layer = Dialogs.Ask("CAD to Detail Lines",
                "Only convert layers whose name contains this text (leave empty for all layers):", "");
            if (layer == null) return Result.Cancelled;
            layer = layer.Trim();

            GraphicsStyle style;
            if (!LineStyles.TryChoose(ctx.Doc, "CAD to Detail Lines", out style)) return Result.Cancelled;

            var options = new Options();
            options.ComputeReferences = false;
            options.View = view;

            var curves = new List<Curve>();
            foreach (ImportInstance import in imports)
            {
                GeometryElement geometry = import.get_Geometry(options);
                if (geometry != null)
                    GeometryCurves.Collect(geometry, o => LayerMatches(ctx.Doc, o, layer), curves);
                if (curves.Count > MaxCurves) break;
            }

            if (curves.Count == 0)
            {
                TaskDialog.Show("CAD to Detail Lines",
                    "No lines or arcs were found. Check the layer filter, or the CAD file may only contain text and hatches.");
                return Result.Cancelled;
            }
            if (curves.Count > MaxCurves)
            {
                TaskDialog.Show("CAD to Detail Lines",
                    "This CAD file has more than " + MaxCurves + " lines. Use the layer filter to convert it in parts.");
                return Result.Cancelled;
            }

            int created = 0, skipped = 0, failed = 0;
            ctx.Transact("CAD to detail lines", () =>
            {
                foreach (Curve curve in curves)
                {
                    List<Curve> flat = DetailCurves.Flatten(curve, view);
                    if (flat.Count == 0) { skipped++; continue; }

                    foreach (Curve piece in flat)
                    {
                        try
                        {
                            DetailCurves.Create(ctx.Doc, view, piece, style);
                            created++;
                        }
                        catch (Exception ex)
                        {
                            if (failed == 0) Log.Error(ex);
                            failed++;
                        }
                    }
                }
            });

            string text = created + " detail line(s) created.";
            if (skipped > 0) text += "\n" + skipped + " curve(s) skipped (splines, ellipses or arcs not parallel to this view).";
            if (failed > 0) text += "\n" + failed + " curve(s) failed (see log).";
            text += "\n\nThe CAD import was left in place; delete it when you are happy with the result.";
            TaskDialog.Show("CAD to Detail Lines", text);
            return Result.Succeeded;
        }

        private static bool LayerMatches(Document doc, GeometryObject obj, string filter)
        {
            if (filter.Length == 0) return true;
            var style = doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
            string name = (style != null && style.GraphicsStyleCategory != null) ? style.GraphicsStyleCategory.Name : "";
            return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

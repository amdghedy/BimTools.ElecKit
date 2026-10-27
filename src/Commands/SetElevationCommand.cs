using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Sets the height above the level of the selected conduits, trays, ducts or pipes (both ends).</summary>
    [Transaction(TransactionMode.Manual)]
    public class SetElevationCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var curves = new List<MEPCurve>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var curve = ctx.Doc.GetElement(id) as MEPCurve;
                if (curve != null) curves.Add(curve);
            }
            if (curves.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(e => e is MEPCurve), "Pick conduits or trays, then click Finish"))
                {
                    var curve = ctx.Doc.GetElement(r) as MEPCurve;
                    if (curve != null) curves.Add(curve);
                }
            }
            if (curves.Count == 0) return Result.Cancelled;

            double? height = Dialogs.AskNumber("Set Elevation", "Height above the level for both ends:",
                ElecSettings.Current.EffectiveConduitOffsetMm, false, "mm");
            if (height == null) return Result.Cancelled;
            double mm = height.Value;
            double feet = UnitConv.MmToFeet(mm);

            int changed = 0, failed = 0;
            ctx.Transact("Set elevation", () =>
            {
                foreach (MEPCurve curve in curves)
                {
                    try
                    {
                        Parameter start = curve.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
                        Parameter end = curve.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);
                        if (start == null || end == null || start.IsReadOnly || end.IsReadOnly) { failed++; continue; }
                        start.Set(feet);
                        end.Set(feet);
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        if (failed == 0) Log.Error(ex);
                        failed++;
                    }
                }
            });

            TaskDialog.Show("Set Elevation", changed + " element(s) set to " + UnitConv.Format(mm) + " mm." +
                (failed > 0 ? "\n" + failed + " could not be changed (read-only or constrained by fittings)." : ""));
            return Result.Succeeded;
        }
    }
}

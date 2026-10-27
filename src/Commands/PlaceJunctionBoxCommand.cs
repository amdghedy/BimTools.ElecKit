using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Places a level-based electrical fixture (a junction box family) at each point you click,
    /// at a set height above the plan level. Repeats until Esc.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlaceJunctionBoxCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Place Junction Box", "Open a plan view first.");
                return Result.Cancelled;
            }

            var symbols = new FilteredElementCollector(ctx.Doc)
                .OfCategory(BuiltInCategory.OST_ElectricalFixtures)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName + " : " + s.Name)
                .ToList();
            if (symbols.Count == 0)
            {
                TaskDialog.Show("Place Junction Box", "No electrical fixture types are loaded. Load a junction box family first.");
                return Result.Cancelled;
            }

            var names = symbols.Select(s => s.FamilyName + " : " + s.Name).ToList();
            int index = ListPicker.Pick("Place Junction Box", "Junction box type:", names);
            if (index < 0) return Result.Cancelled;
            FamilySymbol symbol = symbols[index];

            double? offset = Dialogs.AskNumber("Place Junction Box", "Height above the level:",
                ElecSettings.Current.JunctionBoxOffsetMm ?? 2700.0, false, "mm");
            if (offset == null) return Result.Cancelled;
            double offsetMm = offset.Value;
            ElecSettings.Current.LastJunctionBoxType = names[index];
            ElecSettings.Current.JunctionBoxOffsetMm = offsetMm;
            ElecSettings.Current.Save();

            Level level = plan.GenLevel;
            double z = level.Elevation + UnitConv.MmToFeet(offsetMm);

            if (!symbol.IsActive)
                ctx.Transact("Activate junction box type", () => symbol.Activate());

            int placed = 0;
            try
            {
                while (true)
                {
                    XYZ point = ctx.UiDoc.Selection.PickPoint(
                        ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                        "Pick the junction box location. Esc to finish.");
                    try
                    {
                        ctx.Transact("Place junction box", () =>
                            ctx.Doc.Create.NewFamilyInstance(new XYZ(point.X, point.Y, z), symbol, level, StructuralType.NonStructural));
                        placed++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        TaskDialog.Show("Place Junction Box",
                            "Could not place the family: " + ex.Message + "\nOnly level-based families can be placed this way.");
                        break;
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc ends the loop
            }

            return placed > 0 ? Result.Succeeded : Result.Cancelled;
        }
    }
}

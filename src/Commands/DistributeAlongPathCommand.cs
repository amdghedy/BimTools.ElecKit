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
    /// Spaces picked family instances evenly along a path (a line, arc, conduit, tray...).
    /// Heights are kept. The instances keep their current order along the path.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DistributeAlongPathCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Element pathElement = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                new PredicateFilter(e => CurveOf(e) != null), "Pick the path (a line, arc, conduit or tray)"));
            Curve path = CurveOf(pathElement);
            if (path == null || !path.IsBound)
            {
                TaskDialog.Show("Distribute Along Path", "The path must be a bounded line or arc.");
                return Result.Cancelled;
            }

            var instances = new List<FamilyInstance>();
            foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                new PredicateFilter(e => e is FamilyInstance && e.Location is LocationPoint),
                "Pick the elements to distribute, then click Finish"))
            {
                var instance = ctx.Doc.GetElement(r) as FamilyInstance;
                if (instance != null) instances.Add(instance);
            }
            if (instances.Count < 2)
            {
                TaskDialog.Show("Distribute Along Path", "Pick at least two elements.");
                return Result.Cancelled;
            }

            int mode = Dialogs.Choose("Distribute Along Path", "Spacing:", new List<string>
            {
                "First and last element at the two ends of the path",
                "Equal gaps, nothing placed exactly at the ends"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            var ordered = new List<KeyValuePair<double, FamilyInstance>>();
            foreach (FamilyInstance instance in instances)
            {
                XYZ point = ((LocationPoint)instance.Location).Point;
                IntersectionResult hit = path.Project(point);
                double t = hit != null ? path.ComputeNormalizedParameter(hit.Parameter) : 0.0;
                ordered.Add(new KeyValuePair<double, FamilyInstance>(t, instance));
            }
            ordered.Sort((a, b) => a.Key.CompareTo(b.Key));

            int count = ordered.Count;
            ctx.Transact("Distribute along path", () =>
            {
                for (int i = 0; i < count; i++)
                {
                    double t = mode == 0 ? (double)i / (count - 1) : (i + 0.5) / count;
                    XYZ target = path.Evaluate(t, true);
                    var location = (LocationPoint)ordered[i].Value.Location;
                    location.Point = new XYZ(target.X, target.Y, location.Point.Z);
                }
            });

            TaskDialog.Show("Distribute Along Path", count + " element(s) distributed.");
            return Result.Succeeded;
        }

        private static Curve CurveOf(Element element)
        {
            var curveElement = element as CurveElement;
            if (curveElement != null) return curveElement.GeometryCurve;
            var location = element.Location as LocationCurve;
            return location != null ? location.Curve : null;
        }
    }
}

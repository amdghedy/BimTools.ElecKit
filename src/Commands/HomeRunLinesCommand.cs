using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Draws home-run detail lines from picked devices to a panel, either one line per device
    /// or a chain through the devices in pick order ending at the panel. Plan views only.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class HomeRunLinesCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (!(view is ViewPlan))
            {
                TaskDialog.Show("Home Run Lines", "Open a plan view first.");
                return Result.Cancelled;
            }

            var filter = new PredicateFilter(e => e is FamilyInstance && Anchors.Of(e) != null);
            Reference panelRef = ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                "Pick the panel (or equipment) the home runs go to");
            Element panel = ctx.Doc.GetElement(panelRef);

            IList<Reference> deviceRefs = ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                "Pick the devices in order, then click Finish");
            List<Element> devices = deviceRefs
                .Where(r => r.ElementId.Val() != panel.Id.Val())
                .Select(r => ctx.Doc.GetElement(r))
                .ToList();
            if (devices.Count == 0)
            {
                TaskDialog.Show("Home Run Lines", "Pick at least one device besides the panel.");
                return Result.Cancelled;
            }

            int mode = Dialogs.Choose("Home Run Lines", "Layout:", new List<string>
            {
                "One line from each device to the panel",
                "Chain the devices in pick order, then the last one to the panel"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            int shape = Dialogs.Choose("Home Run Lines", "Route shape:", new List<string>
            {
                "Direct (straight line)",
                "L shape: along X first, then Y",
                "L shape: along Y first, then X"
            }, 1);
            if (shape < 0) return Result.Cancelled;

            GraphicsStyle style;
            if (!LineStyles.TryChoose(ctx.Doc, "Home Run Lines", out style)) return Result.Cancelled;

            XYZ panelPoint = Anchors.Of(panel);
            var pairs = new List<XYZ[]>();
            if (mode == 0)
            {
                foreach (Element device in devices) pairs.Add(new[] { Anchors.Of(device), panelPoint });
            }
            else
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    XYZ origin = Anchors.Of(devices[i]);
                    XYZ destination = i < devices.Count - 1 ? Anchors.Of(devices[i + 1]) : panelPoint;
                    pairs.Add(new[] { origin, destination });
                }
            }

            int created = 0, failed = 0;
            ctx.Transact("Home run lines", () =>
            {
                foreach (XYZ[] pair in pairs)
                {
                    List<XYZ> route = Route(pair[0], pair[1], shape);
                    for (int i = 0; i < route.Count - 1; i++)
                    {
                        if (route[i].DistanceTo(route[i + 1]) < FilletMath.ShortCurveFeet) continue;

                        foreach (Curve piece in DetailCurves.Flatten(Line.CreateBound(route[i], route[i + 1]), view))
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
                }
            });

            string text = created + " detail line(s) drawn.";
            if (failed > 0) text += "\n" + failed + " line(s) failed (see log).";
            TaskDialog.Show("Home Run Lines", text);
            return Result.Succeeded;
        }

        private static List<XYZ> Route(XYZ a, XYZ b, int shape)
        {
            var route = new List<XYZ> { a };
            double dx = Math.Abs(b.X - a.X);
            double dy = Math.Abs(b.Y - a.Y);
            if (shape != 0 && dx > FilletMath.ShortCurveFeet && dy > FilletMath.ShortCurveFeet)
                route.Add(shape == 1 ? new XYZ(b.X, a.Y, a.Z) : new XYZ(a.X, b.Y, a.Z));
            route.Add(b);
            return route;
        }
    }
}

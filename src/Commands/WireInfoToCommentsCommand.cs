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
    /// For each conduit, finds the electrical devices and wires within a set distance (measured in plan)
    /// and writes their circuits and wire types into the conduit's Comments.
    /// Works on the selected conduits, or every conduit in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class WireInfoToCommentsCommand : ElecCommand
    {
        private sealed class Candidate
        {
            public string Circuit;
            public string WireType;
            public double[] X;
            public double[] Y;
            public double MinX, MaxX, MinY, MaxY;
        }

        private static readonly BuiltInCategory[] DeviceCategories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_NurseCallDevices,
            BuiltInCategory.OST_SecurityDevices, BuiltInCategory.OST_TelephoneDevices
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule)
            {
                TaskDialog.Show("Wire Info to Comments", "Open a plan, section or 3D view first.");
                return Result.Cancelled;
            }

            var selected = ctx.UiDoc.Selection.GetElementIds();
            List<Conduit> conduits;
            if (selected.Count > 0)
                conduits = selected.Select(id => ctx.Doc.GetElement(id)).OfType<Conduit>().ToList();
            else
                conduits = new FilteredElementCollector(ctx.Doc, view.Id).OfClass(typeof(Conduit)).Cast<Conduit>().ToList();

            if (conduits.Count == 0)
            {
                TaskDialog.Show("Wire Info to Comments", "No conduits were found in the selection or in this view.");
                return Result.Cancelled;
            }

            string distanceAnswer = Dialogs.Ask("Wire Info to Comments",
                "Look for devices and wires within this distance of each conduit, in millimetres (measured in plan):",
                Units.Format(Settings.Current.EffectiveNearbyMm));
            if (distanceAnswer == null) return Result.Cancelled;

            double distanceMm;
            if (!Units.TryParseNumber(distanceAnswer, out distanceMm) || distanceMm <= 0)
            {
                TaskDialog.Show("Wire Info to Comments", "Enter a number greater than zero, for example 500.");
                return Result.Cancelled;
            }

            int writeMode = Dialogs.Choose("Wire Info to Comments", "What to do with the existing Comments value?",
                new List<string> { "Replace it", "Append to it" }, 0);
            if (writeMode < 0) return Result.Cancelled;

            Settings.Current.NearbyMm = distanceMm;
            Settings.Current.Save();
            double range = Units.MmToFeet(distanceMm);

            List<Candidate> candidates = GatherCandidates(ctx.Doc, view);
            if (candidates.Count == 0)
            {
                TaskDialog.Show("Wire Info to Comments", "No devices or wires with circuit information were found in this view.");
                return Result.Cancelled;
            }

            int updated = 0, noMatch = 0, readOnly = 0;
            ctx.Transact("Wire info to comments", () =>
            {
                foreach (Conduit conduit in conduits)
                {
                    var curve = ((LocationCurve)conduit.Location).Curve;
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    double minX = Math.Min(p0.X, p1.X) - range, maxX = Math.Max(p0.X, p1.X) + range;
                    double minY = Math.Min(p0.Y, p1.Y) - range, maxY = Math.Max(p0.Y, p1.Y) + range;

                    var circuits = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    var wireTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (Candidate c in candidates)
                    {
                        if (c.MaxX < minX || c.MinX > maxX || c.MaxY < minY || c.MinY > maxY) continue;
                        if (!IsNear(c, p0, p1, range)) continue;

                        if (c.Circuit != null)
                            foreach (string part in c.Circuit.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                                circuits.Add(part);
                        if (c.WireType != null) wireTypes.Add(c.WireType);
                    }

                    if (circuits.Count == 0 && wireTypes.Count == 0) { noMatch++; continue; }

                    string text = "";
                    if (circuits.Count > 0) text = "Circuits: " + string.Join(", ", circuits);
                    if (wireTypes.Count > 0) text += (text.Length > 0 ? " | " : "") + "Wires: " + string.Join(", ", wireTypes);

                    Parameter comments = conduit.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comments == null || comments.IsReadOnly) { readOnly++; continue; }

                    string existing = comments.AsString();
                    if (writeMode == 1 && !string.IsNullOrWhiteSpace(existing)) text = existing + " | " + text;
                    comments.Set(text);
                    updated++;
                }
            });

            string result = updated + " conduit(s) updated.";
            if (noMatch > 0) result += "\n" + noMatch + " conduit(s) had nothing within " + Units.Format(distanceMm) + " mm.";
            if (readOnly > 0) result += "\n" + readOnly + " conduit(s) have a read-only Comments parameter.";
            TaskDialog.Show("Wire Info to Comments", result);
            return Result.Succeeded;
        }

        private static List<Candidate> GatherCandidates(Document doc, View view)
        {
            var list = new List<Candidate>();

            var filter = new ElementMulticategoryFilter(DeviceCategories.ToList());
            foreach (FamilyInstance instance in new FilteredElementCollector(doc, view.Id)
                .WherePasses(filter).WhereElementIsNotElementType().OfType<FamilyInstance>())
            {
                string circuit = CircuitInfo.Of(instance);
                XYZ anchor = Anchors.Of(instance);
                if (circuit == null || anchor == null) continue;
                list.Add(Make(circuit, null, new List<XYZ> { anchor }));
            }

            foreach (Wire wire in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Wire)).Cast<Wire>())
            {
                string circuit = CircuitInfo.Of(wire);
                string wireType = wire.WireType != null ? wire.WireType.Name : null;
                if (circuit == null && wireType == null) continue;

                // sample the vertices and the middle of each segment
                var points = new List<XYZ>();
                for (int i = 0; i < wire.NumberOfVertices; i++)
                {
                    points.Add(wire.GetVertex(i));
                    if (i > 0) points.Add(wire.GetVertex(i - 1).Add(wire.GetVertex(i)).Multiply(0.5));
                }
                if (points.Count > 0) list.Add(Make(circuit, wireType, points));
            }
            return list;
        }

        private static Candidate Make(string circuit, string wireType, List<XYZ> points)
        {
            var c = new Candidate
            {
                Circuit = circuit,
                WireType = wireType,
                X = points.Select(p => p.X).ToArray(),
                Y = points.Select(p => p.Y).ToArray()
            };
            c.MinX = c.X.Min(); c.MaxX = c.X.Max();
            c.MinY = c.Y.Min(); c.MaxY = c.Y.Max();
            return c;
        }

        private static bool IsNear(Candidate c, XYZ a, XYZ b, double range)
        {
            for (int i = 0; i < c.X.Length; i++)
            {
                if (Geom2D.PointToSegment(c.X[i], c.Y[i], a.X, a.Y, b.X, b.Y) <= range) return true;
            }
            return false;
        }
    }
}

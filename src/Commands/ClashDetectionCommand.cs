using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Screens host-host and host-linked MEP bounding-box candidates, then checks positive-volume solid intersections.
    /// Linked geometry is transformed into host coordinates; geometry failures are marked for manual review.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ClashDetectionCommand : ElecCommand
    {
        private const int MaxElements = 5000;
        private const double MinOverlapFeet = 0.003280839895; // approximately 1 mm

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_Wire, BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_TelephoneDevices, BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns
        };

        private sealed class Item
        {
            public Element Element;
            public BoundingBoxXYZ Box;
            public string Category;
            public string Source;
            public string ElementId;
            public List<Solid> Solids;
            public bool IsLinked;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (ctx.Doc.ActiveView is ViewSchedule)
            {
                TaskDialog.Show("Clash Review", "Open a model view and run the command again.");
                return Result.Cancelled;
            }

            var items = new List<Item>();
            CollectHostItems(ctx.Doc, ctx.Doc.ActiveView, items);
            int linkCount = 0;
            foreach (RevitLinkInstance link in new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                Document linkedDoc = null;
                try
                {
                    linkedDoc = link.GetLinkDocument();
                    if (linkedDoc == null) continue; // unloaded link
                    Transform transform = link.GetTotalTransform();
                    int before = items.Count;
                    CollectLinkedItems(linkedDoc, link, transform, items);
                    linkCount++;
                    if (items.Count > MaxElements)
                    {
                        TaskDialog.Show("Clash Review", "Host and linked models contain more than " + MaxElements +
                            " candidate elements. Narrow the host view/categories or unload unnecessary links and retry.");
                        return Result.Cancelled;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            if (items.Count < 2)
            {
                TaskDialog.Show("Clash Review", "Fewer than two supported host/linked elements with bounding boxes were found.");
                return Result.Succeeded;
            }
            if (items.Count > MaxElements)
            {
                TaskDialog.Show("Clash Review", "The candidate count exceeds the safety limit of " + MaxElements + ".");
                return Result.Cancelled;
            }

            var clashes = new List<string[]>();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    // Do not report link-to-link pairs; each link is compared against host elements.
                    if (items[i].IsLinked && items[j].IsLinked) continue;
                    if (!Overlaps(items[i].Box, items[j].Box)) continue;

                    string method = SolidIntersectionStatus(items[i].Solids, items[j].Solids);
                    clashes.Add(new[] {
                        items[i].Source, items[i].ElementId, items[i].Category, items[i].Element.Name ?? "",
                        items[j].Source, items[j].ElementId, items[j].Category, items[j].Element.Name ?? "", method
                    });
                }
            }

            if (clashes.Count == 0)
            {
                TaskDialog.Show("Clash Review", "No bounding-box overlap candidates were found. Checked host elements and " +
                    linkCount + " loaded linked model(s).");
                return Result.Succeeded;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export Host and Linked Clash Candidates",
                Filter = "CSV (*.csv)|*.csv",
                FileName = "ElecKit_ClashCandidates_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.AppendLine("Source A,ElementId A,Category A,Name A,Source B,ElementId B,Category B,Name B,Geometry Check");
            foreach (string[] row in clashes)
                csv.AppendLine(string.Join(",", row.Select(Csv.Escape)));
            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Clash Review", clashes.Count + " host-host/host-linked bounding-box overlap candidates exported. " +
                "Positive-volume solid intersections are confirmed where Revit geometry permits; unavailable or failed checks need manual review. " +
                "This is not a clearance check.\\n\\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }

        private static void CollectHostItems(Document doc, View view, List<Item> items)
        {
            foreach (BuiltInCategory category in Categories)
            {
                try
                {
                    foreach (Element e in new FilteredElementCollector(doc, view.Id)
                        .OfCategory(category).WhereElementIsNotElementType())
                    {
                        BoundingBoxXYZ box = e.get_BoundingBox(view);
                        if (box == null) continue;
                        items.Add(new Item
                        {
                            Element = e, Box = ToAxisAlignedBox(box, Transform.Identity),
                            Category = LabelUtils.GetLabelFor(category), Source = doc.Title,
                            ElementId = e.Id.IntegerValue.ToString(CultureInfo.InvariantCulture),
                            Solids = GetSolids(e, Transform.Identity), IsLinked = false
                        });
                        if (items.Count > MaxElements) return;
                    }
                }
                catch (Exception ex) { Log.Error(ex); }
            }
        }

        private static void CollectLinkedItems(Document linkedDoc, RevitLinkInstance link, Transform transform, List<Item> items)
        {
            foreach (BuiltInCategory category in Categories)
            {
                try
                {
                    foreach (Element e in new FilteredElementCollector(linkedDoc)
                        .OfCategory(category).WhereElementIsNotElementType())
                    {
                        BoundingBoxXYZ localBox = e.get_BoundingBox(null);
                        if (localBox == null) continue;
                        BoundingBoxXYZ hostBox = ToAxisAlignedBox(localBox, transform);
                        if (hostBox == null) continue;
                        items.Add(new Item
                        {
                            Element = e, Box = hostBox, Category = LabelUtils.GetLabelFor(category),
                            Source = link.Name + " [" + linkedDoc.Title + "]",
                            ElementId = e.Id.IntegerValue.ToString(CultureInfo.InvariantCulture),
                            Solids = GetSolids(e, transform), IsLinked = true
                        });
                        if (items.Count > MaxElements) return;
                    }
                }
                catch (Exception ex) { Log.Error(ex); }
            }
        }

        private static BoundingBoxXYZ ToAxisAlignedBox(BoundingBoxXYZ box, Transform linkTransform)
        {
            if (box == null) return null;
            Transform boxTransform = box.Transform ?? Transform.Identity;
            XYZ min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            XYZ max = new XYZ(double.MinValue, double.MinValue, double.MinValue);
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                XYZ p = new XYZ(x == 0 ? box.Min.X : box.Max.X,
                                y == 0 ? box.Min.Y : box.Max.Y,
                                z == 0 ? box.Min.Z : box.Max.Z);
                p = linkTransform.OfPoint(boxTransform.OfPoint(p));
                min = new XYZ(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new XYZ(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
            return new BoundingBoxXYZ { Min = min, Max = max, Transform = Transform.Identity };
        }

        private static List<Solid> GetSolids(Element element, Transform transform)
        {
            var solids = new List<Solid>();
            try
            {
                var options = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry != null) CollectSolids(geometry, transform, solids);
            }
            catch (Exception ex) { Log.Error(ex); }
            return solids;
        }

        private static void CollectSolids(GeometryElement geometry, Transform transform, List<Solid> solids)
        {
            foreach (GeometryObject obj in geometry)
            {
                Solid solid = obj as Solid;
                if (solid != null)
                {
                    try
                    {
                        if (solid.Faces.Size > 0 && solid.Volume > 1e-9)
                        {
                            Solid hostSolid = transform.IsIdentity ? solid : SolidUtils.CreateTransformed(solid, transform);
                            if (hostSolid != null && hostSolid.Faces.Size > 0 && hostSolid.Volume > 1e-9)
                                solids.Add(hostSolid);
                        }
                    }
                    catch (Exception ex) { Log.Error(ex); }
                    continue;
                }

                GeometryInstance instance = obj as GeometryInstance;
                if (instance != null)
                {
                    try
                    {
                        GeometryElement nested = instance.GetInstanceGeometry();
                        if (nested != null) CollectSolids(nested, transform, solids);
                    }
                    catch (Exception ex) { Log.Error(ex); }
                }
            }
        }

        private static string SolidIntersectionStatus(List<Solid> a, List<Solid> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0)
                return "Solid check unavailable - manual review";
            bool operationFailed = false;
            foreach (Solid solidA in a)
            foreach (Solid solidB in b)
            {
                try
                {
                    Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                        solidA, solidB, BooleanOperationsType.Intersect);
                    if (intersection != null && intersection.Volume > 1e-9)
                        return "Solid intersection confirmed";
                }
                catch (Exception ex)
                {
                    operationFailed = true;
                    Log.Error(ex);
                }
            }
            return operationFailed ? "Solid check incomplete - manual review" : "No positive-volume solid intersection";
        }

        private static bool Overlaps(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            XYZ amin = a.Min, amax = a.Max, bmin = b.Min, bmax = b.Max;
            return Math.Min(amax.X, bmax.X) - Math.Max(amin.X, bmin.X) > MinOverlapFeet &&
                   Math.Min(amax.Y, bmax.Y) - Math.Max(amin.Y, bmin.Y) > MinOverlapFeet &&
                   Math.Min(amax.Z, bmax.Z) - Math.Max(amin.Z, bmin.Z) > MinOverlapFeet;
        }
    }
}
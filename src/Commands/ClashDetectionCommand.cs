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
    /// Screens host-host, host-linked and linked-linked MEP bounding-box candidates, then checks positive-volume solid intersections.
    /// Linked geometry is transformed into host coordinates; geometry failures are marked for manual review.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ClashDetectionCommand : ElecCommand
    {
        private const int MaxElements = 5000;
        private const double MinOverlapFeet = 0.003280839895; // approximately 1 mm

        private static readonly BuiltInCategory[] MepCategories =
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
            BuiltInCategory.OST_PipeFitting
        };

        private static readonly BuiltInCategory[] CoordinationCategories =
        {
            BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralColumns
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

            var filterDialog = new TaskDialog("Clash Review");
            filterDialog.MainInstruction = "Choose the category scope for clash screening.";
            filterDialog.MainContent = "The report identifies bounding-box overlap candidates, checks solid intersections where possible, and can screen bounding-box separation below a threshold.";
            filterDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "MEP systems only",
                "Electrical, conduit, cable tray, duct and pipe categories.");
            filterDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "MEP + architecture / structure",
                "Also include walls, floors, structural framing and columns.");
            filterDialog.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult filterChoice = filterDialog.Show();
            if (filterChoice != TaskDialogResult.CommandLink1 && filterChoice != TaskDialogResult.CommandLink2)
                return Result.Cancelled;

            string clearanceText = Dialogs.Ask("Clash Review", "Optional bounding-box clearance threshold in millimetres. Enter 0 to report overlaps only:", "0");
            if (clearanceText == null) return Result.Cancelled;
            double clearanceMm;
            if (!double.TryParse(clearanceText, NumberStyles.Float, CultureInfo.CurrentCulture, out clearanceMm) &&
                !double.TryParse(clearanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out clearanceMm))
            {
                TaskDialog.Show("Clash Review", "Enter a valid numeric clearance in millimetres.");
                return Result.Cancelled;
            }
            if (double.IsNaN(clearanceMm) || double.IsInfinity(clearanceMm) || clearanceMm < 0)
            {
                TaskDialog.Show("Clash Review", "Clearance must be a finite, non-negative number.");
                return Result.Cancelled;
            }
            double clearanceFeet = LengthUnits.MillimetresToFeet(clearanceMm);

            BuiltInCategory[] selectedCategories = filterChoice == TaskDialogResult.CommandLink1
                ? MepCategories
                : MepCategories.Concat(CoordinationCategories).ToArray();

            var items = new List<Item>();
            CollectHostItems(ctx.Doc, ctx.Doc.ActiveView, items, selectedCategories);
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
                    CollectLinkedItems(linkedDoc, link, transform, items, selectedCategories);
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
                    // All items are normalized to host coordinates, so link-to-link comparisons are valid.
                    bool overlap = Overlaps(items[i].Box, items[j].Box);
                    double boxDistance = BoxDistance(items[i].Box, items[j].Box);
                    if (!overlap && (clearanceFeet <= 0 || boxDistance > clearanceFeet)) continue;

                    string method = overlap
                        ? SolidIntersectionStatus(items[i].Solids, items[j].Solids)
                        : "Within requested bounding-box clearance; exact solid distance not calculated";
                    clashes.Add(new[] {
                        items[i].Source, items[i].ElementId, items[i].Category, items[i].Element.Name ?? "",
                        items[j].Source, items[j].ElementId, items[j].Category, items[j].Element.Name ?? "", method,
                        LengthUnits.FeetToMillimetres(boxDistance).ToString("0.##", CultureInfo.InvariantCulture),
                        clearanceMm.ToString("0.##", CultureInfo.InvariantCulture)
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
            csv.AppendLine("Source A,ElementId A,Category A,Name A,Source B,ElementId B,Category B,Name B,Geometry Check,Bounding Box Distance (mm),Requested Threshold (mm)");
            foreach (string[] row in clashes)
                csv.AppendLine(string.Join(",", row.Select(Csv.Escape)));
            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Clash Review", clashes.Count + " host-host/host-linked/linked-linked bounding-box overlap candidates exported. " +
                "Positive-volume solid intersections are confirmed where Revit geometry permits; unavailable or failed checks need manual review. " +
                "Bounding-box distances are broad-phase estimates only, not exact solid clearances.\\n\\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }

        private static void CollectHostItems(Document doc, View view, List<Item> items, BuiltInCategory[] categories)
        {
            foreach (BuiltInCategory category in categories)
            {
                if (items.Count > MaxElements) return;
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
                            ElementId = RevitApiCompat.ElementIdValue(e.Id).ToString(CultureInfo.InvariantCulture),
                            Solids = GetSolids(e, Transform.Identity), IsLinked = false
                        });
                        if (items.Count > MaxElements) return;
                    }
                }
                catch (Exception ex) { Log.Error(ex); }
            }
        }

        private static void CollectLinkedItems(Document linkedDoc, RevitLinkInstance link, Transform transform, List<Item> items, BuiltInCategory[] categories)
        {
            foreach (BuiltInCategory category in categories)
            {
                if (items.Count > MaxElements) return;
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
                            ElementId = RevitApiCompat.ElementIdValue(e.Id).ToString(CultureInfo.InvariantCulture),
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

        private static double BoxDistance(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            return BoxClearance.Distance(
                a.Min.X, a.Min.Y, a.Min.Z, a.Max.X, a.Max.Y, a.Max.Z,
                b.Min.X, b.Min.Y, b.Min.Z, b.Max.X, b.Max.Y, b.Max.Z);
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
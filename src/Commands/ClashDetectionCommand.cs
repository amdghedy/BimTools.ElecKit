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
    /// Reports possible clashes from intersecting element bounding boxes in the active view.
    /// These are candidates for review, not proof of exact solid intersection.
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
            BuiltInCategory.OST_PipeFitting
        };

        private sealed class Item
        {
            public Element Element;
            public BoundingBoxXYZ Box;
            public string Category;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (ctx.Doc.ActiveView is ViewSchedule)
            { TaskDialog.Show("Clash Review", "Open a model view and run the command again."); return Result.Cancelled; }

            var items = new List<Item>();
            foreach (BuiltInCategory cat in Categories)
            {
                try
                {
                    foreach (Element e in new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                        .OfCategory(cat).WhereElementIsNotElementType())
                    {
                        BoundingBoxXYZ box = e.get_BoundingBox(ctx.Doc.ActiveView);
                        if (box == null) continue;
                        items.Add(new Item { Element = e, Box = box, Category = LabelUtils.GetLabelFor(cat) });
                    }
                }
                catch (Exception ex) { Log.Error(ex); }
            }

            if (items.Count < 2)
            { TaskDialog.Show("Clash Review", "Fewer than two supported MEP elements with bounding boxes were found in the active view."); return Result.Succeeded; }
            if (items.Count > MaxElements)
            {
                TaskDialog.Show("Clash Review", "The active view contains " + items.Count + " candidate elements. Limit is " +
                    MaxElements + " to keep pairwise checking responsive. Narrow the view or use a selection first.");
                return Result.Cancelled;
            }

            var clashes = new List<string[]>();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (Overlaps(items[i].Box, items[j].Box))
                    {
                        Element a = items[i].Element, b = items[j].Element;
                        clashes.Add(new[] {
                            a.Id.IntegerValue.ToString(CultureInfo.InvariantCulture), items[i].Category, a.Name ?? "",
                            b.Id.IntegerValue.ToString(CultureInfo.InvariantCulture), items[j].Category, b.Name ?? ""
                        });
                    }
                }
            }

            if (clashes.Count == 0)
            { TaskDialog.Show("Clash Review", "No bounding-box overlap candidates were found in the active view."); return Result.Succeeded; }

            var dlg = new SaveFileDialog { Title = "Export Clash Candidates", Filter = "CSV (*.csv)|*.csv",
                FileName = "ElecKit_ClashCandidates_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true, DefaultExt = ".csv" };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.AppendLine("ElementId A,Category A,Name A,ElementId B,Category B,Name B,Detection Method");
            foreach (string[] row in clashes)
                csv.AppendLine(string.Join(",", row.Select(Csv.Escape).Concat(new[] { Csv.Escape("Bounding-box overlap candidate; verify exact geometry") })));
            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Clash Review", clashes.Count + " bounding-box overlap candidates exported. This is a screening report, not a confirmed solid-clash report.\n\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
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
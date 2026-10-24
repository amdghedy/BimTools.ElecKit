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
    [Transaction(TransactionMode.ReadOnly)]
    public class QuantityTakeoffCommand : ElecCommand
    {
        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_Wire, BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_TelephoneDevices, BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_CommunicationDevices
        };

        private sealed class TakeoffRow
        {
            public string Category, Family, Type, Level;
            public int Quantity;
            public double LengthMm;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (ctx.Doc.ActiveView is ViewSchedule)
            { TaskDialog.Show("Quantity Takeoff", "Open a model view and run the command again."); return Result.Cancelled; }

            var rows = new List<TakeoffRow>();
            foreach (BuiltInCategory cat in Categories)
            {
                try
                {
                    var elements = new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                        .OfCategory(cat).WhereElementIsNotElementType().ToElements();
                    foreach (var group in elements.GroupBy(e => new {
                        Category = LabelUtils.GetLabelFor(cat),
                        Family = e is FamilyInstance ? (((FamilyInstance)e).Symbol == null ? "" : ((FamilyInstance)e).Symbol.FamilyName) : "",
                        Type = ctx.Doc.GetElement(e.GetTypeId()) == null ? e.Name : ctx.Doc.GetElement(e.GetTypeId()).Name,
                        Level = LevelName(e)
                    }))
                    {
                        double length = 0;
                        foreach (Element e in group)
                        {
                            Parameter p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                            if (p != null && p.StorageType == StorageType.Double) length += p.AsDouble() * 304.8;
                        }
                        rows.Add(new TakeoffRow { Category = group.Key.Category, Family = group.Key.Family,
                            Type = group.Key.Type, Level = group.Key.Level, Quantity = group.Count(), LengthMm = length });
                    }
                }
                catch (Exception ex) { Log.Error(ex); }
            }
            if (rows.Count == 0)
            { TaskDialog.Show("Quantity Takeoff", "No supported electrical elements were found in the active view."); return Result.Succeeded; }

            var dlg = new SaveFileDialog { Title = "Export Electrical Quantity Takeoff", Filter = "CSV (*.csv)|*.csv",
                FileName = "ElecKit_QuantityTakeoff_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true, DefaultExt = ".csv" };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.AppendLine("Category,Family,Type,Level,Quantity,Total Length (mm)");
            foreach (var r in rows.OrderBy(x => x.Category).ThenBy(x => x.Family).ThenBy(x => x.Type).ThenBy(x => x.Level))
                csv.AppendLine(string.Join(",", Csv.Escape(r.Category), Csv.Escape(r.Family), Csv.Escape(r.Type),
                    Csv.Escape(r.Level), r.Quantity.ToString(CultureInfo.InvariantCulture),
                    r.LengthMm.ToString("0.##", CultureInfo.InvariantCulture)));
            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Quantity Takeoff", rows.Count + " grouped rows exported. Lengths are in millimetres; quantities are instance counts.\n\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }

        private static string LevelName(Element e)
        {
            Parameter p = e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (p == null) return "";
            if (p.StorageType == StorageType.ElementId)
            {
                ElementId id = p.AsElementId();
                return id == ElementId.InvalidElementId ? "" : id.IntegerValue.ToString(CultureInfo.InvariantCulture);
            }
            return p.AsValueString() ?? p.AsString() ?? "";
        }
    }
}
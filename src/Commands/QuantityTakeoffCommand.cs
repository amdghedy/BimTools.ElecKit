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
        private static readonly BuiltInCategory[] ElectricalCategories =
        {
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_Wire, BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_TelephoneDevices, BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_CommunicationDevices
        };

        private static readonly BuiltInCategory[] AdditionalMepCategories =
        {
            BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (ctx.Doc.ActiveView is ViewSchedule)
            { TaskDialog.Show("Quantity Takeoff", "Open a model view and run the command again."); return Result.Cancelled; }

            var scope = new TaskDialog("Quantity Takeoff");
            scope.MainInstruction = "Choose the quantity takeoff scope.";
            scope.MainContent = "Exports grouped instance counts and curve lengths in millimetres from the active view.";
            scope.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Electrical categories",
                "Electrical equipment/devices, conduit, cable tray and wire.");
            scope.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "All supported MEP categories",
                "Electrical categories plus mechanical equipment, ductwork and pipework.");
            scope.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult scopeResult = scope.Show();
            if (scopeResult != TaskDialogResult.CommandLink1 && scopeResult != TaskDialogResult.CommandLink2)
                return Result.Cancelled;
            BuiltInCategory[] selectedCategories = scopeResult == TaskDialogResult.CommandLink1
                ? ElectricalCategories
                : ElectricalCategories.Concat(AdditionalMepCategories).ToArray();

            var items = new List<QuantityTakeoffItem>();
            var categoryErrors = new List<string>();
            foreach (BuiltInCategory cat in selectedCategories)
            {
                try
                {
                    var elements = new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                        .OfCategory(cat).WhereElementIsNotElementType().ToElements();
                    foreach (Element e in elements)
                    {
                        Element typeElement = ctx.Doc.GetElement(e.GetTypeId());
                        string family = e is FamilyInstance
                            ? (((FamilyInstance)e).Symbol == null ? "" : ((FamilyInstance)e).Symbol.FamilyName) : "";
                        string type = typeElement == null ? e.Name : typeElement.Name;
                        Parameter p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                        double lengthMm = p != null && p.StorageType == StorageType.Double
                            ? LengthUnits.FeetToMillimetres(p.AsDouble()) : 0.0;
                        items.Add(new QuantityTakeoffItem {
                            Category = LabelUtils.GetLabelFor(cat), Family = family,
                            Type = type, Level = LevelName(ctx.Doc, e), LengthMm = lengthMm
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    categoryErrors.Add(cat.ToString() + ": " + ex.Message);
                }
            }
            IList<QuantityTakeoffGroup> rows = QuantityTakeoffGrouping.Group(items);
            if (rows.Count == 0)
            {
                string emptyMessage = "No supported elements were found in the active view for the selected scope.";
                if (categoryErrors.Count > 0)
                    emptyMessage += "\n\nCategories that could not be scanned:\n" + string.Join("\n", categoryErrors.Take(10));
                TaskDialog.Show("Quantity Takeoff", emptyMessage);
                return categoryErrors.Count > 0 ? Result.Failed : Result.Succeeded;
            }

            var dlg = new SaveFileDialog { Title = "Export Electrical Quantity Takeoff", Filter = "CSV (*.csv)|*.csv",
                FileName = "ElecKit_QuantityTakeoff_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true, DefaultExt = ".csv" };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.AppendLine("Category,Family,Type,Level,Quantity,Total Length (mm)");
            foreach (var r in rows)
                csv.AppendLine(string.Join(",", Csv.Escape(r.Category), Csv.Escape(r.Family), Csv.Escape(r.Type),
                    Csv.Escape(r.Level), r.Quantity.ToString(CultureInfo.InvariantCulture),
                    r.LengthMm.ToString("0.##", CultureInfo.InvariantCulture)));
            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Quantity Takeoff", rows.Count + " grouped rows exported. Lengths are in millimetres; quantities are instance counts.\n\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }

        private static string LevelName(Document doc, Element e)
        {
            Parameter p = e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (p == null) return "";
            if (p.StorageType == StorageType.ElementId)
            {
                ElementId id = p.AsElementId();
                if (id == ElementId.InvalidElementId) return "";
                Level level = doc.GetElement(id) as Level;
                return level == null ? id.IntegerValue.ToString(CultureInfo.InvariantCulture) : level.Name;
            }
            return p.AsValueString() ?? p.AsString() ?? "";
        }
    }
}
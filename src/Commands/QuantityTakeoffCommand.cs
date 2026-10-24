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
            var categoryAudits = new List<CategoryAudit>();
            foreach (BuiltInCategory cat in selectedCategories)
            {
                try
                {
                    var elements = new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                        .OfCategory(cat).WhereElementIsNotElementType().ToElements();
                    var audit = new CategoryAudit
                    {
                        Category = LabelUtils.GetLabelFor(cat),
                        ElementsFound = elements.Count,
                        ElementsWithLength = 0,
                        TotalLengthMm = 0.0,
                        Status = "OK"
                    };
                    foreach (Element e in elements)
                    {
                        Element typeElement = ctx.Doc.GetElement(e.GetTypeId());
                        string family = e is FamilyInstance
                            ? (((FamilyInstance)e).Symbol == null ? "" : ((FamilyInstance)e).Symbol.FamilyName) : "";
                        string type = typeElement == null ? e.Name : typeElement.Name;
                        Parameter p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                        double lengthMm = p != null && p.StorageType == StorageType.Double
                            ? LengthUnits.FeetToMillimetres(p.AsDouble()) : 0.0;
                        if (p != null && p.StorageType == StorageType.Double)
                        {
                            audit.ElementsWithLength++;
                            audit.TotalLengthMm += lengthMm;
                        }
                        items.Add(new QuantityTakeoffItem {
                            Category = LabelUtils.GetLabelFor(cat), Family = family,
                            Type = type, Level = LevelName(ctx.Doc, e), LengthMm = lengthMm
                        });
                    }
                    categoryAudits.Add(audit);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    string categoryName;
                    try { categoryName = LabelUtils.GetLabelFor(cat); }
                    catch { categoryName = cat.ToString(); }
                    categoryErrors.Add(categoryName + ": " + ex.Message);
                    categoryAudits.Add(new CategoryAudit
                    {
                        Category = categoryName,
                        ElementsFound = 0,
                        ElementsWithLength = 0,
                        TotalLengthMm = 0.0,
                        Status = "ERROR: " + ex.Message
                    });
                }
            }
            IList<QuantityTakeoffGroup> rows = QuantityTakeoffGrouping.Group(items);
            if (rows.Count == 0)
            {
                string emptyMessage = "No supported elements were found in the active view for the selected scope.";
                var auditSave = new SaveFileDialog
                {
                    Title = "Save empty-model category verification report",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = "ElecKit_QuantityTakeoff_CategoryAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                    AddExtension = true,
                    DefaultExt = ".csv"
                };
                if (auditSave.ShowDialog() == true)
                {
                    var emptyAuditCsv = new StringBuilder();
                    emptyAuditCsv.AppendLine("Category,Elements Found,Elements With Length,Total Length (mm),Status");
                    foreach (CategoryAudit audit in categoryAudits)
                        emptyAuditCsv.AppendLine(string.Join(",",
                            Csv.Escape(audit.Category),
                            audit.ElementsFound.ToString(CultureInfo.InvariantCulture),
                            audit.ElementsWithLength.ToString(CultureInfo.InvariantCulture),
                            audit.TotalLengthMm.ToString("0.##", CultureInfo.InvariantCulture),
                            Csv.Escape(audit.Status)));
                    File.WriteAllText(auditSave.FileName, emptyAuditCsv.ToString(), new UTF8Encoding(true));
                    emptyMessage += "\n\nCategory audit saved to:\n" + auditSave.FileName;
                }
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

            string auditPath = Path.Combine(Path.GetDirectoryName(dlg.FileName),
                Path.GetFileNameWithoutExtension(dlg.FileName) + "_CategoryAudit.csv");
            var auditCsv = new StringBuilder();
            auditCsv.AppendLine("Category,Elements Found,Elements With Length,Total Length (mm),Status");
            foreach (CategoryAudit audit in categoryAudits)
                auditCsv.AppendLine(string.Join(",",
                    Csv.Escape(audit.Category),
                    audit.ElementsFound.ToString(CultureInfo.InvariantCulture),
                    audit.ElementsWithLength.ToString(CultureInfo.InvariantCulture),
                    audit.TotalLengthMm.ToString("0.##", CultureInfo.InvariantCulture),
                    Csv.Escape(audit.Status)));
            File.WriteAllText(auditPath, auditCsv.ToString(), new UTF8Encoding(true));

            int scannedCount = categoryAudits.Count(a => a.Status == "OK");
            int elementsFoundTotal = categoryAudits.Where(a => a.Status == "OK").Sum(a => a.ElementsFound);
            double lengthTotal = categoryAudits.Where(a => a.Status == "OK").Sum(a => a.TotalLengthMm);
            string summary = rows.Count + " grouped rows exported. Lengths are in millimetres; quantities are instance counts.\n" +
                "Categories scanned successfully: " + scannedCount + "/" + selectedCategories.Length + "\n" +
                "Elements found across categories: " + elementsFoundTotal + "\n" +
                "Sum of reported curve lengths: " + lengthTotal.ToString("0.##", CultureInfo.InvariantCulture) + " mm\n\n" +
                "Quantity CSV: " + dlg.FileName + "\nCategory audit CSV: " + auditPath;
            if (categoryErrors.Count > 0)
                summary += "\n\nWARNING: " + categoryErrors.Count + " category scan(s) failed; the export is partial.\n" +
                    string.Join("\n", categoryErrors.Take(10));
            TaskDialog.Show("Quantity Takeoff", summary);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return categoryErrors.Count > 0 ? Result.Failed : Result.Succeeded;
        }

        private sealed class CategoryAudit
        {
            public string Category;
            public int ElementsFound;
            public int ElementsWithLength;
            public double TotalLengthMm;
            public string Status;
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
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Sets where reports are saved, whether they open automatically, and the default fillet radius.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class OptionsCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            string folder = Dialogs.Ask("ElecKit Options",
                "Folder for reports and exports (leave empty to use the Desktop):",
                ElecSettings.Current.ReportFolder ?? "");
            if (folder == null) return Result.Cancelled;
            folder = folder.Trim();
            if (folder.Length > 0 && !Directory.Exists(folder))
            {
                TaskDialog.Show("ElecKit Options", "That folder does not exist:\n" + folder);
                return Result.Cancelled;
            }

            int open = Dialogs.Choose("ElecKit Options", "Open reports automatically after they are saved?",
                new List<string> { "Yes", "No" }, ElecSettings.Current.DoNotOpenReports ? 1 : 0);
            if (open < 0) return Result.Cancelled;

            string radius = Dialogs.Ask("ElecKit Options", "Default fillet radius in millimetres:",
                UnitConv.Format(ElecSettings.Current.EffectiveFilletRadiusMm));
            if (radius == null) return Result.Cancelled;
            double radiusMm;
            if (!UnitConv.TryParseNumber(radius, out radiusMm) || radiusMm <= 0)
            {
                TaskDialog.Show("ElecKit Options", "The fillet radius must be a number greater than zero.");
                return Result.Cancelled;
            }

            ElecSettings.Current.ReportFolder = folder.Length == 0 ? null : folder;
            ElecSettings.Current.DoNotOpenReports = open == 1;
            ElecSettings.Current.FilletRadiusMm = radiusMm;
            ElecSettings.Current.Save();

            TaskDialog.Show("ElecKit Options", "Options saved.\n\nReports go to: " + ElecSettings.Current.ResolveReportFolder() +
                "\nSettings file: " + ElecSettings.FilePath);
            return Result.Succeeded;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Saves every model warning to a CSV (description, severity, element ids) and shows the most frequent ones.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class WarningsExportCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            IList<FailureMessage> warnings = ctx.Doc.GetWarnings();
            if (warnings.Count == 0)
            {
                TaskDialog.Show("Model Warnings", "The model has no warnings.");
                return Result.Succeeded;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Severity,Description,Elements,Additional elements");
            foreach (FailureMessage warning in warnings)
            {
                csv.AppendLine(string.Join(",",
                    Csv.Escape(warning.GetSeverity().ToString()),
                    Csv.Escape(warning.GetDescriptionText()),
                    Csv.Escape(string.Join(";", warning.GetFailingElements().Select(id => id.Val().ToString()))),
                    Csv.Escape(string.Join(";", warning.GetAdditionalElements().Select(id => id.Val().ToString())))));
            }
            string path = Reports.PathFor("ModelWarnings", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            var top = warnings
                .GroupBy(w => w.GetDescriptionText())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            var text = new StringBuilder();
            text.AppendLine("MODEL WARNINGS: " + warnings.Count + " in total");
            text.AppendLine();
            text.AppendLine("Most frequent:");
            foreach (var item in top)
                text.AppendLine("  " + item.Count().ToString().PadLeft(5) + "   " + item.Key);
            text.AppendLine();
            text.AppendLine("Full list: " + path);

            Dialogs.ShowText("Model Warnings", text.ToString());
            return Result.Succeeded;
        }
    }
}

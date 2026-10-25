using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CircuitReportCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var systems = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .OrderBy(s => s.PanelName)
                .ThenBy(s => s.CircuitNumber)
                .ToList();

            if (systems.Count == 0)
            {
                TaskDialog.Show("Circuit Report", "No electrical circuits were found in this model.");
                return Result.Succeeded;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Panel,Circuit,Load Name,Elements");
            foreach (var s in systems)
            {
                sb.AppendLine(string.Join(",",
                    Csv.Escape(s.PanelName),
                    Csv.Escape(s.CircuitNumber),
                    Csv.Escape(s.LoadName),
                    s.Elements.Size.ToString()));
            }

            string file = Path.Combine(
                ElecSettings.Current.ResolveReportFolder(),
                "CircuitReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));

            TaskDialog.Show("Circuit Report", systems.Count + " circuits exported to:\n" + file);
            if (!ElecSettings.Current.DoNotOpenReports) Shell.Open(file);
            return Result.Succeeded;
        }
    }
}

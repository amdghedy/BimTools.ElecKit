using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Opens the ElecKit log file, which records the details of any error.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class OpenLogCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (!File.Exists(Log.FilePath))
            {
                TaskDialog.Show("ElecKit Log", "The log is empty: no errors have been recorded yet.\n\n" + Log.FilePath);
                return Result.Succeeded;
            }
            Shell.Open(Log.FilePath);
            return Result.Succeeded;
        }
    }
}

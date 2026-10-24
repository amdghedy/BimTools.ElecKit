using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class FilletRadiusCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            string answer = Dialogs.Ask(
                "Fillet Radius",
                "Default fillet radius in millimetres (used by the fillet tools):",
                Units.Format(Settings.Current.EffectiveFilletRadiusMm));
            if (answer == null) return Result.Cancelled;

            double mm;
            if (!Units.TryParseNumber(answer, out mm) || mm <= 0)
            {
                TaskDialog.Show("Fillet Radius", "Enter a number greater than zero.");
                return Result.Cancelled;
            }

            Settings.Current.FilletRadiusMm = mm;
            Settings.Current.Save();
            TaskDialog.Show("Fillet Radius", "Default fillet radius is now " + Units.Format(mm) + " mm.");
            return Result.Succeeded;
        }
    }
}

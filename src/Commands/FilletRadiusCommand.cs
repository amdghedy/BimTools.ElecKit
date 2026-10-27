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
            double? radius = Dialogs.AskNumber("Fillet Radius", "Default fillet radius used by the fillet tools:",
                ElecSettings.Current.EffectiveFilletRadiusMm, true, "mm");
            if (radius == null) return Result.Cancelled;
            double mm = radius.Value;

            ElecSettings.Current.FilletRadiusMm = mm;
            ElecSettings.Current.Save();
            TaskDialog.Show("Fillet Radius", "Default fillet radius is now " + UnitConv.Format(mm) + " mm.");
            return Result.Succeeded;
        }
    }
}

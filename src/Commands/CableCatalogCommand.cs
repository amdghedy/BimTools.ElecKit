using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Opens the cable catalog (stored in %APPDATA%\ElecKit\cables.json) for review and editing.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class CableCatalogCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<CableDefinition> edited = CableCatalogWindow.Edit(CableCatalogStore.Load());
            if (edited == null) return Result.Cancelled;

            CableCatalogStore.Save(edited);
            TaskDialog.Show("Cable Catalog", edited.Count + " cable(s) saved to:\n" + CableCatalogStore.FilePath);
            return Result.Succeeded;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Creates a new sheet and puts the active view (or schedule) on it, centred.</summary>
    [Transaction(TransactionMode.Manual)]
    public class SheetCreatorCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSheet)
            {
                TaskDialog.Show("Sheet Creator", "Open the view or schedule you want to place first (not a sheet).");
                return Result.Cancelled;
            }

            var schedule = view as ViewSchedule;
            if (schedule == null && !CanPlace(ctx.Doc, view))
            {
                TaskDialog.Show("Sheet Creator", "This view cannot be placed on a sheet, or it is already on one.");
                return Result.Cancelled;
            }

            var titleBlocks = new FilteredElementCollector(ctx.Doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName + " : " + s.Name)
                .ToList();

            ElementId titleBlockId = ElementId.InvalidElementId;
            if (titleBlocks.Count > 0)
            {
                var names = titleBlocks.Select(s => s.FamilyName + " : " + s.Name).ToList();
                int index = ListPicker.Pick("Sheet Creator", "Title block:", names);
                if (index < 0) return Result.Cancelled;
                titleBlockId = titleBlocks[index].Id;
            }

            string number = Dialogs.Ask("Sheet Creator", "Sheet number:", "E-101");
            if (number == null) return Result.Cancelled;
            string name = Dialogs.Ask("Sheet Creator", "Sheet name:", view.Name);
            if (name == null) return Result.Cancelled;

            ViewSheet sheet = null;
            try
            {
                ctx.Transact("Sheet creator", () =>
                {
                    sheet = ViewSheet.Create(ctx.Doc, titleBlockId);
                    sheet.SheetNumber = number.Trim();
                    sheet.Name = name.Trim();

                    BoundingBoxUV outline = sheet.Outline;
                    var center = new XYZ((outline.Min.U + outline.Max.U) / 2.0, (outline.Min.V + outline.Max.V) / 2.0, 0.0);

                    if (schedule != null) ScheduleSheetInstance.Create(ctx.Doc, sheet.Id, schedule.Id, center);
                    else Viewport.Create(ctx.Doc, sheet.Id, view.Id, center);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                TaskDialog.Show("Sheet Creator", "The sheet could not be created: " + ex.Message +
                    "\nCheck that the sheet number is not already used.");
                return Result.Failed;
            }

            ctx.UiDoc.ActiveView = sheet;
            return Result.Succeeded;
        }

        private static bool CanPlace(Document doc, View view)
        {
            // a throw-away check: any sheet id works because the rule is about the view
            var sheetId = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).FirstElementId();
            if (sheetId == null || RevitApiCompat.ElementIdValue(sheetId) == RevitApiCompat.ElementIdValue(ElementId.InvalidElementId)) return true;
            return Viewport.CanAddViewToSheet(doc, sheetId, view.Id);
        }
    }
}

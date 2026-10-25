using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Aligns picked family instances to a reference instance: rotation, location, or one axis. Heights are kept.</summary>
    [Transaction(TransactionMode.Manual)]
    public class AlignToReferenceCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var filter = new PredicateFilter(e => e is FamilyInstance && e.Location is LocationPoint);

            Element reference = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                "Pick the reference element"));
            var referenceLocation = (LocationPoint)reference.Location;

            var targets = new List<FamilyInstance>();
            foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                "Pick the elements to align, then click Finish"))
            {
                var instance = ctx.Doc.GetElement(r) as FamilyInstance;
                if (instance != null && instance.Id.Val() != reference.Id.Val()) targets.Add(instance);
            }
            if (targets.Count == 0) return Result.Cancelled;

            int action = Dialogs.Choose("Align to Reference", "Align:", new List<string>
            {
                "Rotation only",
                "Location (X and Y) and rotation",
                "Location (X and Y) only",
                "X position only",
                "Y position only"
            }, 1);
            if (action < 0) return Result.Cancelled;

            bool rotate = action == 0 || action == 1;
            bool moveX = action == 1 || action == 2 || action == 3;
            bool moveY = action == 1 || action == 2 || action == 4;

            int aligned = 0;
            ctx.Transact("Align to reference", () =>
            {
                foreach (FamilyInstance instance in targets)
                {
                    var location = (LocationPoint)instance.Location;

                    if (rotate)
                    {
                        double delta = referenceLocation.Rotation - location.Rotation;
                        if (Math.Abs(delta) > 1e-9)
                        {
                            XYZ origin = location.Point;
                            location.Rotate(Line.CreateBound(origin, origin.Add(XYZ.BasisZ)), delta);
                        }
                    }

                    if (moveX || moveY)
                    {
                        XYZ current = location.Point;
                        XYZ target = referenceLocation.Point;
                        location.Point = new XYZ(moveX ? target.X : current.X, moveY ? target.Y : current.Y, current.Z);
                    }
                    aligned++;
                }
            });

            TaskDialog.Show("Align to Reference", aligned + " element(s) aligned.");
            return Result.Succeeded;
        }
    }
}

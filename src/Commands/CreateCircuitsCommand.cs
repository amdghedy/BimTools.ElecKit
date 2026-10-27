using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Creates power circuits from selected fixtures and devices (one circuit for all, or one for each)
    /// and connects them to a panel you pick.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateCircuitsCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var elements = new List<FamilyInstance>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var instance = ctx.Doc.GetElement(id) as FamilyInstance;
                if (IsLoad(instance)) elements.Add(instance);
            }
            if (elements.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(IsLoad), "Pick the fixtures and devices for the circuit(s), then click Finish"))
                {
                    var instance = ctx.Doc.GetElement(r) as FamilyInstance;
                    if (instance != null) elements.Add(instance);
                }
            }
            if (elements.Count == 0) return Result.Cancelled;

            int mode = Dialogs.Choose("Create Circuits", "Create:", new List<string>
            {
                "One circuit for all " + elements.Count + " element(s)",
                "One circuit for each element"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            int panelChoice = Dialogs.Choose("Create Circuits", "Panel:", new List<string>
            {
                "Connect the circuit(s) to a panel (pick it next)",
                "Leave the circuit(s) without a panel"
            }, 0);
            if (panelChoice < 0) return Result.Cancelled;

            FamilyInstance panel = null;
            if (panelChoice == 0)
            {
                panel = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                    new PredicateFilter(IsPanel), "Pick the panel")) as FamilyInstance;
            }

            var groups = new List<List<ElementId>>();
            if (mode == 0) groups.Add(elements.Select(e => e.Id).ToList());
            else foreach (FamilyInstance e in elements) groups.Add(new List<ElementId> { e.Id });

            int created = 0, assigned = 0, panelFailed = 0, failed = 0;
            string firstError = null;
            ctx.Transact("Create circuits", () =>
            {
                foreach (List<ElementId> ids in groups)
                {
                    ElectricalSystem system;
                    try
                    {
                        system = ElectricalSystem.Create(ctx.Doc, ids, ElectricalSystemType.PowerCircuit);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        if (firstError == null) firstError = ex.Message;
                        failed++;
                        continue;
                    }

                    if (panel == null) continue;
                    try
                    {
                        system.SelectPanel(panel);
                        assigned++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        panelFailed++;
                    }
                }
            });

            string text = created + " circuit(s) created.";
            if (panel != null) text += "\n" + assigned + " connected to the panel.";
            if (panelFailed > 0) text += "\n" + panelFailed + " could not be connected to the panel (voltage or phase mismatch?).";
            if (failed > 0) text += "\n" + failed + " could not be created: " + firstError;
            TaskDialog.Show("Create Circuits", text);
            return created > 0 ? Result.Succeeded : Result.Failed;
        }

        private static bool IsLoad(Element e)
        {
            var instance = e as FamilyInstance;
            return instance != null && instance.MEPModel != null && instance.MEPModel.ConnectorManager != null;
        }

        private static bool IsPanel(Element e)
        {
            return e is FamilyInstance && e.Category != null &&
                   e.Category.Id.Val() == (int)BuiltInCategory.OST_ElectricalEquipment;
        }
    }
}

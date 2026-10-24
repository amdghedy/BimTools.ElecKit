using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Places an elbow, transition or tee between selected cable-tray end connectors.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ConnectCableTrayFittingCommand : ElecCommand
    {
        private sealed class TrayFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) { return element is CableTray; }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            int fittingKind = Dialogs.Choose("Connect Cable Tray Fitting", "Choose fitting type and then select trays in order:",
                new System.Collections.Generic.List<string> {
                    "Elbow (2 trays)", "Transition / reducer (2 trays)", "Tee (main run first, branch third)"
                }, 0);
            if (fittingKind < 0 || fittingKind > 2) return Result.Cancelled;

            int required = fittingKind == 2 ? 3 : 2;
            IList<Reference> picked;
            try
            {
                picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element, new TrayFilter(),
                    fittingKind == 2
                        ? "Select exactly three cable trays: main run first, second main segment second, branch third"
                        : "Select exactly two cable trays whose end connectors should meet");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            if (picked == null || picked.Count != required)
            {
                TaskDialog.Show("Connect Cable Tray Fitting", "This fitting requires exactly " + required + " cable tray segment(s). No changes were made.");
                return Result.Cancelled;
            }

            var trays = new List<CableTray>();
            foreach (Reference r in picked)
            {
                CableTray tray = ctx.Doc.GetElement(r) as CableTray;
                if (tray == null) return Result.Cancelled;
                trays.Add(tray);
            }

            Connector[] selectedConnectors = FindNearestConnectorCombination(trays, out double spread);
            if (selectedConnectors == null || spread > UnitConv.MmToFeet(75.0))
            {
                TaskDialog.Show("Connect Cable Tray Fitting",
                    "The selected trays do not have end connectors within 75 mm of a common fitting point. Reposition/extend the tray segments or check their geometry. No changes were made.");
                return Result.Cancelled;
            }

            string failure = null;
            ctx.Transact("Connect cable tray fitting", () =>
            {
                using (var sub = new SubTransaction(ctx.Doc))
                {
                    sub.Start();
                    try
                    {
                        if (fittingKind == 0)
                            ctx.Doc.Create.NewElbowFitting(selectedConnectors[0], selectedConnectors[1]);
                        else if (fittingKind == 1)
                            ctx.Doc.Create.NewTransitionFitting(selectedConnectors[0], selectedConnectors[1]);
                        else
                            ctx.Doc.Create.NewTeeFitting(selectedConnectors[0], selectedConnectors[1], selectedConnectors[2]);
                        sub.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        failure = ex.Message;
                        Log.Error(ex);
                    }
                }
            });

            if (failure != null)
            {
                TaskDialog.Show("Connect Cable Tray Fitting",
                    "Revit rejected the fitting; the fitting transaction was rolled back.\n\n" + failure +
                    "\n\nCheck connector orientation, tray sizes, routing preferences and whether the selected ends are already connected.");
                return Result.Failed;
            }

            TaskDialog.Show("Connect Cable Tray Fitting",
                (fittingKind == 0 ? "Elbow" : fittingKind == 1 ? "Transition/reducer" : "Tee") +
                " fitting created successfully. Verify connector continuity and clearances before issue.");
            return Result.Succeeded;
        }

        private static Connector[] FindNearestConnectorCombination(IList<CableTray> trays, out double maxSpread)
        {
            maxSpread = double.MaxValue;
            var sets = new List<List<Connector>>();
            foreach (CableTray tray in trays)
            {
                var connectors = new List<Connector>();
                foreach (Connector connector in tray.ConnectorManager.Connectors)
                    if (connector.ConnectorType == ConnectorType.End) connectors.Add(connector);
                if (connectors.Count == 0) return null;
                sets.Add(connectors);
            }

            Connector[] best = null;
            if (sets.Count == 2)
            {
                foreach (Connector a in sets[0])
                foreach (Connector b in sets[1])
                {
                    double distance = a.Origin.DistanceTo(b.Origin);
                    if (distance < maxSpread) { maxSpread = distance; best = new[] { a, b }; }
                }
            }
            else
            {
                double bestScore = double.MaxValue;
                foreach (Connector a in sets[0])
                foreach (Connector b in sets[1])
                foreach (Connector c in sets[2])
                {
                    double ab = a.Origin.DistanceTo(b.Origin);
                    double ac = a.Origin.DistanceTo(c.Origin);
                    double bc = b.Origin.DistanceTo(c.Origin);
                    double spread = Math.Max(ab, Math.Max(ac, bc));
                    double score = ab + ac + bc;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        maxSpread = spread;
                        best = new[] { a, b, c };
                    }
                }
            }
            return best;
        }
    }
}
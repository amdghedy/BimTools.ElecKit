using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Colours the devices in the active view by the panel (or circuit) they are on, using view overrides,
    /// and lists which colour is which. Clear Colors removes the overrides again.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ColorByCircuitCommand : ElecCommand
    {
        private static readonly string[] ColorNames =
        {
            "Red", "Blue", "Green", "Orange", "Purple", "Teal", "Magenta", "Brown", "Olive", "Navy", "Pink", "Gray"
        };

        private static readonly byte[][] Rgb =
        {
            new byte[] { 220, 38, 38 }, new byte[] { 37, 99, 235 }, new byte[] { 22, 163, 74 }, new byte[] { 234, 118, 0 },
            new byte[] { 147, 51, 234 }, new byte[] { 13, 148, 136 }, new byte[] { 219, 39, 119 }, new byte[] { 146, 86, 40 },
            new byte[] { 132, 140, 20 }, new byte[] { 30, 58, 138 }, new byte[] { 244, 114, 182 }, new byte[] { 107, 114, 128 }
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule || view is ViewSheet)
            {
                TaskDialog.Show("Color by Circuit", "Open a plan, section, elevation or 3D view first.");
                return Result.Cancelled;
            }

            int mode = Dialogs.Choose("Color by Circuit", "Colour the devices by:", new List<string>
            {
                "Panel (one colour for each panel)",
                "Circuit (one colour for each circuit)"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            List<FamilyInstance> devices = CircuitDevices.InView(ctx.Doc, view);
            if (devices.Count == 0)
            {
                TaskDialog.Show("Color by Circuit", "No devices on a circuit were found in this view.");
                return Result.Cancelled;
            }

            var keyOf = new Dictionary<int, string>();
            foreach (FamilyInstance device in devices)
                keyOf[device.Id.Val()] = mode == 0 ? CircuitDevices.PanelName(device) : CircuitInfo.Of(device);

            List<string> keys = keyOf.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            ElementId solid = SolidFill(ctx.Doc);

            int colored = 0, blocked = 0;
            ctx.Transact("Color by circuit", () =>
            {
                foreach (FamilyInstance device in devices)
                {
                    int index = keys.FindIndex(k => string.Equals(k, keyOf[device.Id.Val()], StringComparison.OrdinalIgnoreCase));
                    byte[] c = Rgb[index % Rgb.Length];
                    var color = new Color(c[0], c[1], c[2]);

                    var overrides = new OverrideGraphicSettings();
                    overrides.SetProjectionLineColor(color);
                    overrides.SetCutLineColor(color);
                    if (solid != null)
                    {
                        overrides.SetSurfaceForegroundPatternId(solid);
                        overrides.SetSurfaceForegroundPatternColor(color);
                    }

                    try
                    {
                        view.SetElementOverrides(device.Id, overrides);
                        colored++;
                    }
                    catch (Exception ex)
                    {
                        if (blocked == 0) Log.Error(ex);
                        blocked++;
                    }
                }
            });

            if (colored == 0)
            {
                TaskDialog.Show("Color by Circuit",
                    "Revit did not allow overrides in this view. A view template may control them: use a view without one, or remove the template.");
                return Result.Failed;
            }

            var legend = new StringBuilder();
            legend.AppendLine("COLOURS IN VIEW: " + view.Name);
            legend.AppendLine();
            for (int i = 0; i < keys.Count; i++)
                legend.AppendLine("  " + ColorNames[i % ColorNames.Length].PadRight(9) + keys[i]);
            if (keys.Count > ColorNames.Length) legend.AppendLine("\n(" + ColorNames.Length + " colours repeat after that.)");
            legend.AppendLine();
            legend.AppendLine(colored + " device(s) coloured. Use Clear Colors to undo.");
            if (blocked > 0) legend.AppendLine(blocked + " could not be coloured.");
            Dialogs.ShowText("Color by Circuit", legend.ToString());
            return Result.Succeeded;
        }

        private static ElementId SolidFill(Document doc)
        {
            foreach (FillPatternElement element in new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)))
            {
                FillPattern pattern = element.GetFillPattern();
                if (pattern != null && pattern.IsSolidFill && pattern.Target == FillPatternTarget.Drafting) return element.Id;
            }
            return null;
        }
    }
}

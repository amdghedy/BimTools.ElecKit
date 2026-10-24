using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace ElecKit.Core
{
    public static class CircuitInfo
    {
        /// <summary>
        /// "Panel-Circuit" text for an element (several circuits are joined with a comma),
        /// or null if it is on no circuit. Works for family instances and for wires.
        /// </summary>
        public static string Of(Element element)
        {
            var parts = new List<string>();

            var instance = element as FamilyInstance;
            if (instance != null && instance.MEPModel != null)
            {
                var systems = instance.MEPModel.GetElectricalSystems();
                if (systems != null)
                {
                    foreach (ElectricalSystem system in systems)
                        Add(parts, system.PanelName, system.CircuitNumber);
                }
            }
            else
            {
                Parameter panel = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_PANEL_PARAM);
                Parameter number = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER);
                Add(parts,
                    panel != null ? panel.AsString() : null,
                    number != null ? number.AsString() : null);
            }

            if (parts.Count == 0) return null;
            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", parts);
        }

        private static void Add(List<string> parts, string panel, string number)
        {
            if (string.IsNullOrEmpty(number)) return;
            string text = string.IsNullOrEmpty(panel) ? number : panel + "-" + number;
            if (!parts.Contains(text)) parts.Add(text);
        }
    }
}

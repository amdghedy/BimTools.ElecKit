using System;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class CircuitInfo
    {
        /// <summary>
        /// "Panel-Circuit" text for an element, read from its Panel and Circuit Number parameters,
        /// or null if it is on no circuit. Works for fixtures, equipment and wires.
        /// </summary>
        public static string Of(Element element)
        {
            Parameter panel = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_PANEL_PARAM);
            Parameter number = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER);

            string panelText = panel != null ? panel.AsString() : null;
            string numberText = number != null ? number.AsString() : null;
            if (string.IsNullOrWhiteSpace(numberText)) return null;

            return string.IsNullOrWhiteSpace(panelText) ? numberText.Trim() : panelText.Trim() + "-" + numberText.Trim();
        }
    }
}

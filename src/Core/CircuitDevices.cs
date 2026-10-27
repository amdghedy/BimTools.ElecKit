using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class CircuitDevices
    {
        /// <summary>Family instances in the view that are on a circuit.</summary>
        public static List<FamilyInstance> InView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(f => f.Category != null && CircuitInfo.Of(f) != null)
                .ToList();
        }

        /// <summary>The panel name of a device, or its circuit text when the panel name is empty.</summary>
        public static string PanelName(Element device)
        {
            Parameter panel = device.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_PANEL_PARAM);
            string name = panel != null ? panel.AsString() : null;
            return string.IsNullOrWhiteSpace(name) ? (CircuitInfo.Of(device) ?? "") : name.Trim();
        }
    }
}

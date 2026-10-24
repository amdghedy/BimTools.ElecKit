using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    /// <summary>Shared "which conduit type, and at what height" questions. Remembers the last answers.</summary>
    public static class ConduitPrompt
    {
        public static bool TryAsk(Document doc, string title, out ConduitType type, out double offsetMm)
        {
            type = null;
            offsetMm = 0;

            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(ConduitType))
                .Cast<ConduitType>()
                .OrderBy(t => t.Name)
                .ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show(title, "This project has no conduit types. Load or create one first.");
                return false;
            }

            int index = 0;
            if (types.Count > 1)
            {
                int last = types.FindIndex(t => t.Name == Settings.Current.LastConduitType);
                index = Dialogs.Choose(title, "Conduit type:", types.Select(t => t.Name).ToList(), last);
                if (index < 0) return false;
            }

            string answer = Dialogs.Ask(title, "Conduit height above the level, in millimetres:",
                Units.Format(Settings.Current.EffectiveConduitOffsetMm));
            if (answer == null) return false;

            if (!Units.TryParseNumber(answer, out offsetMm))
            {
                TaskDialog.Show(title, "Enter a number, for example 2700.");
                return false;
            }

            type = types[index];
            Settings.Current.LastConduitType = type.Name;
            Settings.Current.ConduitOffsetMm = offsetMm;
            Settings.Current.Save();
            return true;
        }
    }
}

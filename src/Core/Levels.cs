using System.Linq;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class Levels
    {
        /// <summary>The highest level at or below z (feet), or the lowest level if z is below all of them.</summary>
        public static Level LevelBelow(Document doc, double z)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
            if (levels.Count == 0) return null;

            Level best = levels[0];
            foreach (Level level in levels)
            {
                if (level.Elevation <= z + 0.01) best = level;
                else break;
            }
            return best;
        }
    }
}

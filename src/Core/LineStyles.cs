using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class LineStyles
    {
        /// <summary>All detail line styles in the project, sorted by name.</summary>
        public static List<GraphicsStyle> All(Document doc)
        {
            var result = new List<GraphicsStyle>();
            Category lines = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (lines == null) return result;

            foreach (Category sub in lines.SubCategories)
            {
                GraphicsStyle style = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                if (style != null && !style.Name.StartsWith("<")) result.Add(style);
            }
            return result.OrderBy(s => s.Name).ToList();
        }

        /// <summary>
        /// Asks which line style to use. style = null means "keep the default".
        /// Returns false if the user cancelled.
        /// </summary>
        public static bool TryChoose(Document doc, string title, out GraphicsStyle style)
        {
            style = null;
            List<GraphicsStyle> all = All(doc);
            if (all.Count == 0) return true;

            var names = new List<string> { "(default line style)" };
            names.AddRange(all.Select(s => s.Name));

            int last = string.IsNullOrEmpty(ElecSettings.Current.LastLineStyle) ? 0 : names.IndexOf(ElecSettings.Current.LastLineStyle);
            int index = Dialogs.Choose(title, "Line style for the new detail lines:", names, Math.Max(last, 0));
            if (index < 0) return false;

            style = index == 0 ? null : all[index - 1];
            ElecSettings.Current.LastLineStyle = index == 0 ? null : names[index];
            ElecSettings.Current.Save();
            return true;
        }
    }
}

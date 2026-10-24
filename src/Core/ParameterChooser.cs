using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    public static class ParameterChooser
    {
        /// <summary>
        /// Lists the writable text parameters of a sample element and asks which one to use.
        /// Returns null if cancelled or if there are none.
        /// </summary>
        public static string Choose(Element sample, string title, string remembered)
        {
            var names = new List<string>();
            foreach (Parameter p in sample.Parameters)
            {
                if (p.StorageType == StorageType.String && !p.IsReadOnly && p.Definition != null &&
                    !names.Contains(p.Definition.Name))
                    names.Add(p.Definition.Name);
            }
            if (names.Count == 0)
            {
                TaskDialog.Show(title, "The first element has no writable text parameter.");
                return null;
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);

            int index = names.IndexOf(remembered ?? "");
            if (index < 0)
            {
                Parameter comments = sample.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comments != null) index = names.IndexOf(comments.Definition.Name);
            }

            int chosen = Dialogs.Choose(title, "Write into this parameter:", names, Math.Max(index, 0));
            return chosen < 0 ? null : names[chosen];
        }
    }
}

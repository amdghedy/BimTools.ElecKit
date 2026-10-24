using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace ElecKit.Core
{
    public sealed class PredicateFilter : ISelectionFilter
    {
        private readonly Func<Element, bool> _allow;

        public PredicateFilter(Func<Element, bool> allow) { _allow = allow; }

        public bool AllowElement(Element elem) { return _allow(elem); }

        public bool AllowReference(Reference reference, XYZ position) { return false; }
    }
}

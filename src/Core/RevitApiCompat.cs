using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    /// <summary>Compatibility helpers for the 64-bit ElementId API introduced in Revit 2026.</summary>
    internal static class RevitApiCompat
    {
        public static long ElementIdValue(ElementId id)
        {
#if REVIT2026_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId CreateElementId(long value)
        {
#if REVIT2026_OR_GREATER
            return new ElementId(value);
#else
            return new ElementId(checked((int)value));
#endif
        }
    }
}

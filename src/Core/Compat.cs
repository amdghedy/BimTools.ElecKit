using System.Diagnostics;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    /// <summary>Smooths over API differences between Revit versions (the build defines REVITnnnn constants).</summary>
    public static class IdExtensions
    {
        /// <summary>The numeric value of an element id (ElementId.IntegerValue was replaced by Value in Revit 2024).</summary>
        public static int Val(this ElementId id)
        {
#if REVIT2024_OR_GREATER
            return (int)id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }

    public static class Shell
    {
        /// <summary>Opens a file or folder with its default program (works on .NET Framework and .NET 8).</summary>
        public static void Open(string path)
        {
            var info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }
}

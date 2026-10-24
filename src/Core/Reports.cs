using System;
using System.Diagnostics;
using System.IO;

namespace ElecKit.Core
{
    public static class Reports
    {
        /// <summary>A new, time-stamped path in the report folder.</summary>
        public static string PathFor(string baseName, string extension)
        {
            return Path.Combine(ElecSettings.Current.ResolveReportFolder(),
                FileNames.Safe(baseName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension);
        }

        public static void OpenIfWanted(string path)
        {
            if (!ElecSettings.Current.DoNotOpenReports) Process.Start(path);
        }
    }
}

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

        /// <summary>
        /// Shows the result window for a tool that saved a file or folder. The window has an Open button;
        /// if you chose to open reports automatically and did not click Open, the file opens when the window closes.
        /// </summary>
        public static void Finish(string title, string summary, System.Collections.Generic.IEnumerable<string> details, string path)
        {
            bool opened = Dialogs.Result(title, summary, details, path);
            if (!opened) OpenIfWanted(path);
        }

        public static void OpenIfWanted(string path)
        {
            if (!ElecSettings.Current.DoNotOpenReports) Shell.Open(path);
        }
    }
}

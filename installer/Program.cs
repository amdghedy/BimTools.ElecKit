using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ElecKit.Installer
{
    internal static class Program
    {
        /// <summary>
        /// No arguments: the setup window.
        /// /install   installs for every Revit version found on this PC (for scripts and IT deployment)
        /// /uninstall removes ElecKit for every version in this setup
        /// /quiet     no message box; the exit code is 0 on success
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            bool install = args.Any(a => string.Equals(a, "/install", StringComparison.OrdinalIgnoreCase));
            bool uninstall = args.Any(a => string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase));
            bool quiet = args.Any(a => string.Equals(a, "/quiet", StringComparison.OrdinalIgnoreCase));

            if (install || uninstall) return RunCommandLine(install, quiet);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static int RunCommandLine(bool install, bool quiet)
        {
            var report = new StringBuilder();
            int failures = 0;
            int handled = 0;

            foreach (int year in Setup.PayloadYears())
            {
                if (install && !Setup.IsRevitInstalled(year)) continue;

                string message;
                bool ok = install ? Setup.Install(year, out message) : Setup.Uninstall(year, out message);
                report.AppendLine(message);
                handled++;
                if (!ok) failures++;
            }

            if (handled == 0) report.AppendLine("No matching Revit versions were found on this PC.");
            if (!quiet) MessageBox.Show(report.ToString(), "ElecKit Setup");
            return failures == 0 && handled > 0 ? 0 : 1;
        }
    }
}

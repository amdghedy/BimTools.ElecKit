using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;
namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class DeploymentAuditCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            string manifest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", "2020", "ElecKit.addin");
            string expectedDll = Path.Combine(Path.GetDirectoryName(manifest), "ElecKit", "ElecKit.dll");
            string loadedDll = typeof(App).Assembly.Location;
            bool manifestExists = File.Exists(manifest), deployedExists = File.Exists(expectedDll), loadedExists = File.Exists(loadedDll);
            string loadedHash = loadedExists ? Hash(loadedDll) : "", deployedHash = deployedExists ? Hash(expectedDll) : "";
            bool hashMatch = loadedExists && deployedExists && string.Equals(loadedHash, deployedHash, StringComparison.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.AppendLine("ElecKit deployment audit — Revit 2020");
            sb.AppendLine("Manifest: " + manifest + " [" + (manifestExists ? "FOUND" : "MISSING") + "]");
            sb.AppendLine("Expected deployed DLL: " + expectedDll + " [" + (deployedExists ? "FOUND" : "MISSING") + "]");
            sb.AppendLine("Currently loaded assembly: " + loadedDll + " [" + (loadedExists ? "FOUND" : "MISSING") + "]");
            if (loadedExists) sb.AppendLine("Loaded DLL SHA-256: " + loadedHash);
            if (deployedExists) sb.AppendLine("Deployed DLL SHA-256: " + deployedHash);
            sb.AppendLine("Loaded/deployed hash match: " + (hashMatch ? "YES" : "NO"));
            sb.AppendLine("Loaded assembly version: " + typeof(App).Assembly.GetName().Version);
            sb.AppendLine("Audit time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            string folder = ElecSettings.Current.ResolveReportFolder();
            Directory.CreateDirectory(folder);
            string report = Path.Combine(folder, "DeploymentAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(report, sb.ToString(), new UTF8Encoding(true));
            Log.Info("Deployment audit: manifest=" + manifestExists + "; deployedDll=" + deployedExists + "; hashMatch=" + hashMatch + "; report=" + report);
            TaskDialog.Show("ElecKit Deployment Audit", sb.ToString() + "\nReport saved to:\n" + report +
                "\n\nA mismatch may be expected when running from a development output folder. Close Revit before replacing a loaded DLL.");
            if (!ElecSettings.Current.DoNotOpenReports) Process.Start(new ProcessStartInfo(report) { UseShellExecute = true });
            return manifestExists && deployedExists && hashMatch ? Result.Succeeded : Result.Failed;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] bytes = sha.ComputeHash(stream);
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
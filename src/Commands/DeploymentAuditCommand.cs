using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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
            string manifestAssemblyValue = "", manifestDll = "", manifestError = "";
            try
            {
                if (manifestExists)
                {
                    XDocument xml = XDocument.Load(manifest);
                    XElement assemblyElement = xml.Root == null ? null : xml.Root.Element("Assembly");
                    manifestAssemblyValue = assemblyElement == null ? "" : (assemblyElement.Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(manifestAssemblyValue))
                        manifestError = "Manifest has no Assembly element/value.";
                    else
                        manifestDll = Path.IsPathRooted(manifestAssemblyValue)
                            ? manifestAssemblyValue
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest), manifestAssemblyValue));
                }
            }
            catch (Exception ex) { manifestError = ex.Message; Log.Error(ex); }
            bool manifestDllExists = !string.IsNullOrWhiteSpace(manifestDll) && File.Exists(manifestDll);
            string loadedHash = loadedExists ? Hash(loadedDll) : "";
            string deployedHash = deployedExists ? Hash(expectedDll) : "";
            string manifestHash = manifestDllExists ? Hash(manifestDll) : "";
            bool loadedMatchesDeployed = loadedExists && deployedExists && string.Equals(loadedHash, deployedHash, StringComparison.OrdinalIgnoreCase);
            bool manifestMatchesExpected = manifestDllExists && string.Equals(Path.GetFullPath(manifestDll), Path.GetFullPath(expectedDll), StringComparison.OrdinalIgnoreCase);
            bool manifestMatchesDeployedHash = manifestDllExists && deployedExists && string.Equals(manifestHash, deployedHash, StringComparison.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.AppendLine("ElecKit deployment audit — Revit 2020");
            sb.AppendLine("Manifest: " + manifest + " [" + (manifestExists ? "FOUND" : "MISSING") + "]");
            sb.AppendLine("Manifest Assembly value: " + (string.IsNullOrWhiteSpace(manifestAssemblyValue) ? "(unreadable)" : manifestAssemblyValue));
            sb.AppendLine("Resolved manifest DLL: " + (string.IsNullOrWhiteSpace(manifestDll) ? "(unresolved)" : manifestDll) + " [" + (manifestDllExists ? "FOUND" : "MISSING") + "]");
            if (!string.IsNullOrWhiteSpace(manifestError)) sb.AppendLine("Manifest parse issue: " + manifestError);
            sb.AppendLine("Expected deployed DLL: " + expectedDll + " [" + (deployedExists ? "FOUND" : "MISSING") + "]");
            sb.AppendLine("Currently loaded assembly: " + loadedDll + " [" + (loadedExists ? "FOUND" : "MISSING") + "]");
            if (loadedExists) sb.AppendLine("Loaded DLL SHA-256: " + loadedHash);
            if (deployedExists) sb.AppendLine("Deployed DLL SHA-256: " + deployedHash);
            if (manifestDllExists) sb.AppendLine("Manifest DLL SHA-256: " + manifestHash);
            sb.AppendLine("Manifest resolves to expected deployment path: " + (manifestMatchesExpected ? "YES" : "NO"));
            sb.AppendLine("Manifest/deployed hash match: " + (manifestMatchesDeployedHash ? "YES" : "NO"));
            sb.AppendLine("Loaded/deployed hash match: " + (loadedMatchesDeployed ? "YES" : "NO"));
            sb.AppendLine("Loaded assembly version: " + typeof(App).Assembly.GetName().Version);
            sb.AppendLine("Audit time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            string folder = ElecSettings.Current.ResolveReportFolder();
            Directory.CreateDirectory(folder);
            string report = Path.Combine(folder, "DeploymentAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(report, sb.ToString(), new UTF8Encoding(true));
            Log.Info("Deployment audit: manifest=" + manifestExists + "; manifestDll=" + manifestDllExists +
                "; deployedDll=" + deployedExists + "; loadedMatchesDeployed=" + loadedMatchesDeployed +
                "; manifestMatchesExpected=" + manifestMatchesExpected + "; report=" + report);
            TaskDialog.Show("ElecKit Deployment Audit", sb.ToString() + "\nReport saved to:\n" + report +
                "\n\nA mismatch may be expected when running from a development output folder. Close Revit before replacing a loaded DLL.");
            if (!ElecSettings.Current.DoNotOpenReports) Process.Start(new ProcessStartInfo(report) { UseShellExecute = true });
            return manifestExists && manifestDllExists && deployedExists && manifestMatchesExpected &&
                manifestMatchesDeployedHash && loadedMatchesDeployed ? Result.Succeeded : Result.Failed;
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
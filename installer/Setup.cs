using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace ElecKit.Installer
{
    /// <summary>Reads the embedded payload and installs or removes ElecKit for one Revit version.</summary>
    internal static class Setup
    {
        private const string PayloadName = "payload.zip";
        private const string AddinFile = "ElecKit.addin";
        private const string FolderName = "ElecKit";

        /// <summary>The Revit versions this setup contains (the top-level folders of the payload).</summary>
        public static List<int> PayloadYears()
        {
            using (ZipArchive zip = OpenPayload())
            {
                var years = new List<int>();
                if (zip == null) return years;

                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    int slash = name.IndexOf('/');
                    if (slash <= 0) continue;

                    int year;
                    if (int.TryParse(name.Substring(0, slash), out year) && !years.Contains(year)) years.Add(year);
                }
                years.Sort();
                return years;
            }
        }

        public static bool IsRevitInstalled(int year)
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(programFiles)) programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            return Directory.Exists(Path.Combine(programFiles, "Autodesk", "Revit " + year))
                || Directory.Exists(Path.Combine(programData, "Autodesk", "Revit", "Addins", year.ToString()))
                || Directory.Exists(AddinsFolder(year));
        }

        public static bool Install(int year, out string message)
        {
            try
            {
                using (ZipArchive zip = OpenPayload())
                {
                    if (zip == null) { message = "This setup file contains no files."; return false; }

                    ZipArchiveEntry manifest = zip.GetEntry(AddinFile);
                    if (manifest == null) { message = "The add-in manifest is missing from this setup file."; return false; }

                    string addins = AddinsFolder(year);
                    string target = Path.Combine(addins, FolderName);
                    Directory.CreateDirectory(target);

                    string prefix = year + "/";
                    int copied = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        if (name.EndsWith("/", StringComparison.Ordinal)) continue;
                        if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;

                        string destination = Path.Combine(target, name.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        Extract(entry, destination);
                        copied++;
                    }
                    if (copied == 0) { message = "Revit " + year + ": this setup has no files for that version."; return false; }

                    Extract(manifest, Path.Combine(addins, AddinFile));
                    message = "Revit " + year + ": installed (" + copied + " file(s)).";
                    return true;
                }
            }
            catch (IOException ex)
            {
                message = "Revit " + year + ": could not write the files (" + ex.Message + "). Close Revit and run the setup again.";
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "Revit " + year + ": access denied (" + ex.Message + ").";
                return false;
            }
        }

        public static bool Uninstall(int year, out string message)
        {
            try
            {
                string addins = AddinsFolder(year);
                string manifest = Path.Combine(addins, AddinFile);
                string folder = Path.Combine(addins, FolderName);

                bool found = File.Exists(manifest) || Directory.Exists(folder);
                if (File.Exists(manifest)) File.Delete(manifest);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);

                message = found ? "Revit " + year + ": removed." : "Revit " + year + ": nothing to remove.";
                return true;
            }
            catch (IOException ex)
            {
                message = "Revit " + year + ": could not remove the files (" + ex.Message + "). Close Revit and try again.";
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "Revit " + year + ": access denied (" + ex.Message + ").";
                return false;
            }
        }

        private static string AddinsFolder(int year)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", year.ToString());
        }

        private static ZipArchive OpenPayload()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName);
            return stream == null ? null : new ZipArchive(stream, ZipArchiveMode.Read);
        }

        private static void Extract(ZipArchiveEntry entry, string destination)
        {
            using (Stream input = entry.Open())
            using (FileStream output = File.Create(destination))
                input.CopyTo(output);
        }
    }
}

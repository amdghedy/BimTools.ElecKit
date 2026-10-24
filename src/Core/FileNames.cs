using System.IO;

namespace ElecKit.Core
{
    public static class FileNames
    {
        /// <summary>Replaces characters that are not allowed in Windows file names.</summary>
        public static string Safe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}

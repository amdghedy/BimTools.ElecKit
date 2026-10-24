using System;
using System.IO;

namespace ElecKit.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ElecKit", "log.txt");
            }
        }

        public static void Info(string message) { Write("INFO ", message); }

        public static void Error(Exception ex) { Write("ERROR", ex.ToString()); }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message + Environment.NewLine);
                }
            }
            catch
            {
                // logging must never break a command
            }
        }
    }
}

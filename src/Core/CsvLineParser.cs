using System;
using System.Collections.Generic;
using System.Text;

namespace ElecKit.Core
{
    /// <summary>Parses one RFC-4180-style CSV record, including escaped quotes and embedded delimiters.</summary>
    public static class CsvLineParser
    {
        public static string[] Parse(string line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));

            var result = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    result.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(ch);
                }
            }

            if (quoted)
                throw new FormatException("CSV record contains an unterminated quoted field.");

            result.Add(cell.ToString());
            return result.ToArray();
        }
    }
}
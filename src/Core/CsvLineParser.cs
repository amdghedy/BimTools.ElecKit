using System;
using System.Collections.Generic;
using System.Text;

namespace ElecKit.Core
{
    /// <summary>Parses one RFC-4180-style CSV record, including escaped quotes and embedded delimiters.</summary>
    public static class CsvLineParser
    {
        /// <summary>Parses a complete CSV document, preserving commas, escaped quotes and newlines inside quoted cells.</summary>
        public static List<string[]> ParseRecords(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var records = new List<string[]>();
            var row = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                row.Append(ch);
                if (ch == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        row.Append(text[++i]);
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if ((ch == '\r' || ch == '\n') && !quoted)
                {
                    row.Length--;
                    string record = row.ToString();
                    row.Clear();
                    if (record.Length > 0)
                        records.Add(Parse(record));
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
            }
            if (quoted) throw new FormatException("CSV document contains an unterminated quoted field.");
            if (row.Length > 0)
                records.Add(Parse(row.ToString()));
            return records;
        }

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
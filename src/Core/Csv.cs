using System.Collections.Generic;
using System.Text;

namespace ElecKit.Core
{
    public static class Csv
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        /// <summary>Uses ';' when the first line has more semicolons than commas, otherwise ','.</summary>
        public static char DetectDelimiter(string text)
        {
            int end = text.IndexOf('\n');
            string first = end < 0 ? text : text.Substring(0, end);
            int commas = 0, semicolons = 0;
            foreach (char c in first)
            {
                if (c == ',') commas++;
                else if (c == ';') semicolons++;
            }
            return semicolons > commas ? ';' : ',';
        }

        /// <summary>Parses CSV text (quoted cells, doubled quotes, CRLF). Blank lines are dropped.</summary>
        public static List<string[]> Parse(string text)
        {
            text = text.TrimStart('\uFEFF');
            char delimiter = DetectDelimiter(text);

            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { EndRow(rows, row, cell); }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) EndRow(rows, row, cell);
            return rows;
        }

        private static void EndRow(List<string[]> rows, List<string> row, StringBuilder cell)
        {
            row.Add(cell.ToString());
            cell.Clear();
            bool blank = row.Count == 1 && row[0].Length == 0;
            if (!blank) rows.Add(row.ToArray());
            row.Clear();
        }
    }
}

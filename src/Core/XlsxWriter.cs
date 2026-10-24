using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ElecKit.Core
{
    /// <summary>Writes a plain .xlsx workbook (text and number cells) using only the .NET zip classes.</summary>
    public static class XlsxWriter
    {
        public static void Write(string path, IList<KeyValuePair<string, List<string[]>>> sheets)
        {
            if (sheets.Count == 0) throw new ArgumentException("No sheets to write.");
            var names = UniqueSheetNames(sheets);

            using (var file = new FileStream(path, FileMode.Create))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                var types = new StringBuilder();
                types.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                types.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
                types.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                types.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                types.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                for (int i = 1; i <= sheets.Count; i++)
                    types.Append("<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                types.Append("</Types>");
                AddEntry(zip, "[Content_Types].xml", types.ToString());

                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                var workbook = new StringBuilder();
                workbook.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                workbook.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                var rels = new StringBuilder();
                rels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                rels.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Count; i++)
                {
                    int n = i + 1;
                    workbook.Append("<sheet name=\"" + SecurityElement.Escape(names[i]) + "\" sheetId=\"" + n + "\" r:id=\"rId" + n + "\"/>");
                    rels.Append("<Relationship Id=\"rId" + n + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + n + ".xml\"/>");
                }
                workbook.Append("</sheets></workbook>");
                rels.Append("</Relationships>");
                AddEntry(zip, "xl/workbook.xml", workbook.ToString());
                AddEntry(zip, "xl/_rels/workbook.xml.rels", rels.ToString());

                for (int i = 0; i < sheets.Count; i++)
                    AddEntry(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", SheetXml(sheets[i].Value));
            }
        }

        private static string SheetXml(List<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append("<row r=\"" + (r + 1) + "\">");
                string[] cells = rows[r];
                for (int c = 0; c < cells.Length; c++)
                {
                    string text = cells[c] ?? "";
                    if (text.Length == 0) continue;
                    string reference = ColumnName(c) + (r + 1);

                    double number;
                    bool plain = text.Trim() == text && !(text.Length > 1 && text[0] == '0' && char.IsDigit(text[1]));
                    if (plain && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                        && !double.IsInfinity(number) && !double.IsNaN(number))
                    {
                        sb.Append("<c r=\"" + reference + "\"><v>" + number.ToString("R", CultureInfo.InvariantCulture) + "</v></c>");
                    }
                    else
                    {
                        sb.Append("<c r=\"" + reference + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" +
                                  SecurityElement.Escape(StripInvalidXml(text)) + "</t></is></c>");
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string ColumnName(int index)
        {
            string name = "";
            int n = index;
            do
            {
                name = (char)('A' + n % 26) + name;
                n = n / 26 - 1;
            } while (n >= 0);
            return name;
        }

        private static string StripInvalidXml(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\t' || c == '\n' || c == '\r' || (c >= ' ' && c != '\uFFFE' && c != '\uFFFF')) sb.Append(c);
            }
            return sb.ToString();
        }

        private static List<string> UniqueSheetNames(IList<KeyValuePair<string, List<string[]>>> sheets)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var sheet in sheets)
            {
                string name = (sheet.Key ?? "Sheet").Trim();
                foreach (char bad in new[] { '[', ']', ':', '*', '?', '/', '\\' }) name = name.Replace(bad, '_');
                name = name.Trim('\'');
                if (name.Length == 0) name = "Sheet";
                if (name.Length > 31) name = name.Substring(0, 31);

                string candidate = name;
                int suffix = 2;
                while (!used.Add(candidate))
                {
                    string tail = "_" + suffix++;
                    candidate = (name.Length + tail.Length > 31 ? name.Substring(0, 31 - tail.Length) : name) + tail;
                }
                result.Add(candidate);
            }
            return result;
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }
    }
}

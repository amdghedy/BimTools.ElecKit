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
    }
}

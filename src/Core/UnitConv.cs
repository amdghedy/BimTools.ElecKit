using System.Globalization;

namespace ElecKit.Core
{
    public static class UnitConv
    {
        public const double MmPerFoot = 304.8;

        public static double MmToFeet(double mm) { return mm / MmPerFoot; }

        public static double FeetToMm(double feet) { return feet * MmPerFoot; }

        /// <summary>Parses "150", "150.5" or "150,5".</summary>
        public static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return double.TryParse(text.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

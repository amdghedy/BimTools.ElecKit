using System;
using System.Collections.Generic;
using System.Linq;

namespace ElecKit.Core
{
    /// <summary>Revit-independent grouped quantity accumulator used by the takeoff command.</summary>
    public sealed class QuantityTakeoffGroup
    {
        public string Category { get; private set; }
        public string Family { get; private set; }
        public string Type { get; private set; }
        public string Level { get; private set; }
        public int Quantity { get; private set; }
        public double LengthMm { get; private set; }

        public QuantityTakeoffGroup(string category, string family, string type, string level)
        {
            Category = category ?? "";
            Family = family ?? "";
            Type = type ?? "";
            Level = level ?? "";
        }

        public void Add(double lengthMm)
        {
            if (double.IsNaN(lengthMm) || double.IsInfinity(lengthMm))
                throw new ArgumentOutOfRangeException(nameof(lengthMm), "Length must be finite.");
            if (Quantity == int.MaxValue)
                throw new InvalidOperationException("Grouped quantity exceeds the supported integer range.");
            double total = LengthMm + lengthMm;
            if (double.IsInfinity(total))
                throw new ArgumentOutOfRangeException(nameof(lengthMm), "Grouped length overflowed.");
            Quantity++;
            LengthMm = total;
        }
    }

    public static class QuantityTakeoffGrouping
    {
        public static IList<QuantityTakeoffGroup> Group(IEnumerable<QuantityTakeoffItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var groups = new Dictionary<string, QuantityTakeoffGroup>(StringComparer.Ordinal);
            foreach (QuantityTakeoffItem item in items)
            {
                if (item == null) throw new ArgumentException("Items cannot contain null.", nameof(items));
                string key = Encode(item.Category) + Encode(item.Family) + Encode(item.Type) + Encode(item.Level);
                QuantityTakeoffGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new QuantityTakeoffGroup(item.Category, item.Family, item.Type, item.Level);
                    groups.Add(key, group);
                }
                group.Add(item.LengthMm);
            }
            return groups.Values.OrderBy(g => g.Category, StringComparer.Ordinal)
                .ThenBy(g => g.Family, StringComparer.Ordinal)
                .ThenBy(g => g.Type, StringComparer.Ordinal)
                .ThenBy(g => g.Level, StringComparer.Ordinal).ToList();
        }

        private static string Encode(string value)
        {
            value = value ?? "";
            return value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
        }
    }

    public sealed class QuantityTakeoffItem
    {
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }
        public double LengthMm { get; set; }
    }
}
using System;
using System.Collections.Generic;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class QuantityTakeoffGroupingTests
    {
        [Fact]
        public void AggregatesQuantityAndLengthForMatchingKeys()
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item("Conduit", "Family A", "Type 20", "Level 1", 304.8),
                Item("Conduit", "Family A", "Type 20", "Level 1", 609.6),
                Item("Conduit", "Family A", "Type 20", "Level 1", 0)
            });
            var row = Assert.Single(rows);
            Assert.Equal(3, row.Quantity);
            Assert.Equal(914.4, row.LengthMm, 8);
        }

        [Fact]
        public void SeparatesRowsWhenAnyGroupingDimensionDiffers()
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item("Conduit", "Family A", "Type 20", "Level 1", 10),
                Item("Conduit", "Family A", "Type 20", "Level 2", 20),
                Item("Conduit", "Family A", "Type 25", "Level 1", 30),
                Item("Tray", "Family A", "Type 20", "Level 1", 40)
            });
            Assert.Equal(4, rows.Count);
            Assert.Equal(new[] { 1, 1, 1, 1 }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(rows, x => x.Quantity)));
        }

        [Fact]
        public void DoesNotCollideWhenLabelsContainSeparators()
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item("A|B", "C", "D", "E", 1),
                Item("A", "B|C", "D", "E", 2)
            });
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void RejectsNonFiniteLengths()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                QuantityTakeoffGrouping.Group(new[] { Item("A", "B", "C", "D", double.NaN) }));
        }

        [Fact]
        public void RejectsNullInputAndNullItems()
        {
            Assert.Throws<ArgumentNullException>(() => QuantityTakeoffGrouping.Group(null));
            Assert.Throws<ArgumentException>(() => QuantityTakeoffGrouping.Group(new QuantityTakeoffItem[] { null }));
        }

        private static QuantityTakeoffItem Item(string category, string family, string type, string level, double length)
        {
            return new QuantityTakeoffItem {
                Category = category, Family = family, Type = type, Level = level, LengthMm = length
            };
        }
    }
}
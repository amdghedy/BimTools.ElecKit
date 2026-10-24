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

        [Theory]
        [InlineData("Electrical Equipment")]
        [InlineData("Electrical Fixtures")]
        [InlineData("Lighting Fixtures")]
        [InlineData("Lighting Devices")]
        [InlineData("Conduit")]
        [InlineData("Conduit Fittings")]
        [InlineData("Cable Tray")]
        [InlineData("Cable Tray Fittings")]
        [InlineData("Wires")]
        [InlineData("Data Devices")]
        [InlineData("Telephone Devices")]
        [InlineData("Security Devices")]
        [InlineData("Fire Alarm Devices")]
        [InlineData("Communication Devices")]
        [InlineData("Mechanical Equipment")]
        [InlineData("Ducts")]
        [InlineData("Duct Fittings")]
        [InlineData("Pipes")]
        [InlineData("Pipe Fittings")]
        public void KeepsEachSupportedCategoryInItsOwnGroup(string category)
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item(category, "Family", "Type", "Level", 12.5),
                Item(category, "Family", "Type", "Level", 7.5)
            });
            var row = Assert.Single(rows);
            Assert.Equal(category, row.Category);
            Assert.Equal(2, row.Quantity);
            Assert.Equal(20.0, row.LengthMm, 8);
        }

        [Fact]
        public void KeepsDifferentCategoriesSeparateEvenWhenOtherLabelsMatch()
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item("Conduit", "Family", "Type", "Level", 100),
                Item("Cable Tray", "Family", "Type", "Level", 200),
                Item("Pipe", "Family", "Type", "Level", 300)
            });
            Assert.Equal(3, rows.Count);
            Assert.Equal(600.0, System.Linq.Enumerable.Sum(rows, row => row.LengthMm), 8);
        }

        [Fact]
        public void RejectsNonFiniteLengths()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                QuantityTakeoffGrouping.Group(new[] { Item("A", "B", "C", "D", double.NaN) }));
        }

        [Fact]
        public void PreservesGrandTotalsAcrossManyGroups()
        {
            var items = new List<QuantityTakeoffItem>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(Item(i % 2 == 0 ? "Conduit" : "Cable Tray",
                    "Family " + (i % 5), "Type " + (i % 3), "Level " + (i % 4), i * 1.25));
            }

            var rows = QuantityTakeoffGrouping.Group(items);
            Assert.Equal(100, System.Linq.Enumerable.Sum(rows, row => row.Quantity));
            Assert.Equal(System.Linq.Enumerable.Sum(items, item => item.LengthMm),
                System.Linq.Enumerable.Sum(rows, row => row.LengthMm), 8);
        }

        [Fact]
        public void RejectsOverflowedGroupedLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                QuantityTakeoffGrouping.Group(new[] {
                    Item("Conduit", "Family", "Type", "Level", double.MaxValue),
                    Item("Conduit", "Family", "Type", "Level", double.MaxValue)
                }));
        }

        [Fact]
        public void SortsGroupsDeterministically()
        {
            var rows = QuantityTakeoffGrouping.Group(new[] {
                Item("Wire", "B", "T", "L", 1),
                Item("Conduit", "Z", "T", "L", 1),
                Item("Conduit", "A", "T", "L", 1)
            });
            Assert.Equal(new[] { "Conduit", "Conduit", "Wire" },
                System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(rows, row => row.Category)));
            Assert.Equal("A", rows[0].Family);
            Assert.Equal("Z", rows[1].Family);
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
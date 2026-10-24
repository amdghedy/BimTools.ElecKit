using System;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class LengthUnitsTests
    {
        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1.0, 304.8)]
        [InlineData(10.0, 3048.0)]
        [InlineData(-1.0, -304.8)]
        public void ConvertsFeetToMillimetres(double feet, double expected)
        {
            Assert.Equal(expected, LengthUnits.FeetToMillimetres(feet), 8);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(304.8, 1.0)]
        [InlineData(3048.0, 10.0)]
        [InlineData(-304.8, -1.0)]
        public void ConvertsMillimetresToFeet(double millimetres, double expected)
        {
            Assert.Equal(expected, LengthUnits.MillimetresToFeet(millimetres), 8);
        }

        [Fact]
        public void RejectsNonFiniteLengths()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LengthUnits.FeetToMillimetres(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => LengthUnits.MillimetresToFeet(double.PositiveInfinity));
        }
    }
}
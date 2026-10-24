using System;

namespace ElecKit.Core
{
    /// <summary>Explicit Revit internal length conversions used by reporting code.</summary>
    public static class LengthUnits
    {
        private const double MillimetresPerFoot = 304.8;

        public static double FeetToMillimetres(double feet)
        {
            if (double.IsNaN(feet) || double.IsInfinity(feet))
                throw new ArgumentOutOfRangeException(nameof(feet), "Length must be finite.");
            return feet * MillimetresPerFoot;
        }

        public static double MillimetresToFeet(double millimetres)
        {
            if (double.IsNaN(millimetres) || double.IsInfinity(millimetres))
                throw new ArgumentOutOfRangeException(nameof(millimetres), "Length must be finite.");
            return millimetres / MillimetresPerFoot;
        }
    }
}
using System;

namespace ElecKit.Core
{
    /// <summary>
    /// Minimum Euclidean separation between two axis-aligned boxes.
    /// Inputs and result use the same units. This is a broad-phase distance only,
    /// not an exact distance between Revit solids.
    /// </summary>
    public static class BoxClearance
    {
        public static double Distance(
            double aMinX, double aMinY, double aMinZ, double aMaxX, double aMaxY, double aMaxZ,
            double bMinX, double bMinY, double bMinZ, double bMaxX, double bMaxY, double bMaxZ)
        {
            Validate(aMinX, aMinY, aMinZ, aMaxX, aMaxY, aMaxZ);
            Validate(bMinX, bMinY, bMinZ, bMaxX, bMaxY, bMaxZ);
            if (aMinX > aMaxX || aMinY > aMaxY || aMinZ > aMaxZ ||
                bMinX > bMaxX || bMinY > bMaxY || bMinZ > bMaxZ)
                throw new ArgumentException("Box minimum coordinates must not exceed maximum coordinates.");

            double dx = Gap(aMinX, aMaxX, bMinX, bMaxX);
            double dy = Gap(aMinY, aMaxY, bMinY, bMaxY);
            double dz = Gap(aMinZ, aMaxZ, bMinZ, bMaxZ);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double Gap(double aMin, double aMax, double bMin, double bMax)
        {
            if (aMax < bMin) return bMin - aMax;
            if (bMax < aMin) return aMin - bMax;
            return 0.0;
        }

        private static void Validate(params double[] values)
        {
            foreach (double value in values)
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(values), "Box coordinates must be finite.");
        }
    }
}

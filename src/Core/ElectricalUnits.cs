using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    /// <summary>Converts Revit's internal electrical values (VA, volts) to ordinary numbers on every Revit version.</summary>
    public static class ElectricalUnits
    {
        public static double ToVoltAmperes(double internalValue)
        {
#if REVIT2022_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.VoltAmperes);
#else
            return UnitUtils.ConvertFromInternalUnits(internalValue, DisplayUnitType.DUT_VOLT_AMPERES);
#endif
        }

        public static double ToVolts(double internalValue)
        {
#if REVIT2022_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Volts);
#else
            return UnitUtils.ConvertFromInternalUnits(internalValue, DisplayUnitType.DUT_VOLTS);
#endif
        }
    }
}

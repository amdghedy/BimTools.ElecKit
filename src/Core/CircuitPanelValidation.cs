using System;
using System.Collections.Generic;
using System.Globalization;
namespace ElecKit.Core
{
    public sealed class CircuitPanelCheck
    {
        public bool CanAssign { get; internal set; }
        public IList<string> Errors { get; private set; }
        public IList<string> Warnings { get; private set; }
        public CircuitPanelCheck() { CanAssign = true; Errors = new List<string>(); Warnings = new List<string>(); }
    }
    /// <summary>Conservative preflight. Unknown data warns; known conflicts block assignment.</summary>
    public static class CircuitPanelValidation
    {
        public static CircuitPanelCheck Evaluate(double? circuitVoltage, double? panelVoltage,
            int? circuitPoles, int? panelPoles, double? circuitLoadVa, double? panelCapacityVa)
        {
            var result = new CircuitPanelCheck();
            Compare(result, circuitVoltage, panelVoltage, "Voltage", false);
            Compare(result, circuitPoles.HasValue ? (double?)circuitPoles.Value : null,
                panelPoles.HasValue ? (double?)panelPoles.Value : null, "Pole count", true);
            if (!circuitLoadVa.HasValue || !Finite(circuitLoadVa.Value) || circuitLoadVa.Value < 0)
                result.Warnings.Add("Circuit apparent load is unavailable; capacity was not checked.");
            else if (!panelCapacityVa.HasValue || !Finite(panelCapacityVa.Value) || panelCapacityVa.Value <= 0)
                result.Warnings.Add("No explicit panel capacity in VA/kVA was found; capacity was not checked.");
            else if (circuitLoadVa.Value > panelCapacityVa.Value)
                result.Errors.Add("Circuit apparent load (" + F(circuitLoadVa.Value) +
                    " VA) exceeds declared panel capacity (" + F(panelCapacityVa.Value) + " VA).");
            result.CanAssign = result.Errors.Count == 0;
            return result;
        }
        private static void Compare(CircuitPanelCheck result, double? source, double? target, string label, bool discrete)
        {
            if (!source.HasValue || !target.HasValue || !Finite(source.Value) || !Finite(target.Value) ||
                source.Value <= 0 || target.Value <= 0)
            {
                result.Warnings.Add(label + " could not be verified because one or both values are unavailable.");
                return;
            }
            if (Math.Abs(source.Value - target.Value) > (discrete ? 0.0 : Math.Max(1.0, Math.Max(source.Value, target.Value) * 0.01)))
                result.Errors.Add(label + " mismatch: circuit " + F(source.Value) + " vs panel " + F(target.Value) + ".");
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static string F(double value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
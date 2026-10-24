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
            return Evaluate(circuitVoltage, panelVoltage, circuitPoles, panelPoles, circuitLoadVa, panelCapacityVa, 0.0);
        }
        /// <summary>Existing load is the sum of other circuits assigned to the destination panel. Null means unverified.</summary>
        public static CircuitPanelCheck Evaluate(double? circuitVoltage, double? panelVoltage,
            int? circuitPoles, int? panelPoles, double? circuitLoadVa, double? panelCapacityVa, double? existingPanelLoadVa)
        {
            var result = new CircuitPanelCheck();
            Compare(result, circuitVoltage, panelVoltage, "Voltage", false);
            Compare(result, circuitPoles.HasValue ? (double?)circuitPoles.Value : null,
                panelPoles.HasValue ? (double?)panelPoles.Value : null, "Pole count", true);
            bool validLoad = circuitLoadVa.HasValue && Finite(circuitLoadVa.Value) && circuitLoadVa.Value >= 0;
            bool validCapacity = panelCapacityVa.HasValue && Finite(panelCapacityVa.Value) && panelCapacityVa.Value > 0;
            if (!validLoad) result.Warnings.Add("Circuit apparent load is unavailable; capacity was not checked.");
            if (!validCapacity) result.Warnings.Add("No explicit panel capacity in VA/kVA was found; capacity was not checked.");
            if (validLoad && validCapacity)
            {
                if (existingPanelLoadVa.HasValue && Finite(existingPanelLoadVa.Value) && existingPanelLoadVa.Value >= 0)
                {
                    double projected = existingPanelLoadVa.Value + circuitLoadVa.Value;
                    if (!Finite(projected))
                        result.Warnings.Add("Projected panel load overflowed numeric range; capacity was not checked.");
                    else if (projected > panelCapacityVa.Value)
                        result.Errors.Add("Projected panel load (" + F(projected) + " VA, including existing load " +
                            F(existingPanelLoadVa.Value) + " VA) exceeds declared panel capacity (" + F(panelCapacityVa.Value) + " VA).");
                }
                else
                {
                    if (circuitLoadVa.Value > panelCapacityVa.Value)
                        result.Errors.Add("Circuit apparent load (" + F(circuitLoadVa.Value) +
                            " VA) exceeds declared panel capacity (" + F(panelCapacityVa.Value) + " VA).");
                    result.Warnings.Add(existingPanelLoadVa == null
                        ? "Existing destination-panel load could not be verified; total panel capacity remains unconfirmed."
                        : "Existing destination-panel load is invalid; total panel capacity remains unconfirmed.");
                }
            }
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
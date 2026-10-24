using Xunit;
using ElecKit.Core;
namespace ElecKit.Tests
{
    public class CircuitPanelValidationTests
    {
        [Fact] public void KnownVoltageMismatchBlocksAssignment()
        {
            var result = CircuitPanelValidation.Evaluate(400, 230, 3, 3, 1200, 10000);
            Assert.False(result.CanAssign);
            Assert.Contains(result.Errors, x => x.Contains("Voltage mismatch"));
        }
        [Fact] public void OnePoleDifferenceIsDetected()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 2, 1, 1200, 10000);
            Assert.False(result.CanAssign);
            Assert.Contains(result.Errors, x => x.Contains("Pole count mismatch"));
        }
        [Fact] public void LoadAboveExplicitCapacityBlocksAssignment()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 1, 1, 12000, 10000);
            Assert.False(result.CanAssign);
            Assert.Contains(result.Errors, x => x.Contains("exceeds declared panel capacity"));
        }
        [Fact] public void ExistingPanelLoadPlusNewCircuitCannotExceedCapacity()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 1, 1, 2500, 10000, 8000);
            Assert.False(result.CanAssign);
            Assert.Contains(result.Errors, x => x.Contains("Projected panel load"));
        }
        [Fact] public void ProjectedPanelLoadAtCapacityPasses()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 1, 1, 2000, 10000, 8000);
            Assert.True(result.CanAssign);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }
        [Fact] public void UnknownExistingPanelLoadIsNotTreatedAsZero()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 1, 1, 2000, 10000, null);
            Assert.True(result.CanAssign);
            Assert.Contains(result.Warnings, x => x.Contains("Existing destination-panel load could not be verified"));
        }
        [Fact] public void MissingRatingsWarnInsteadOfAssumingCompatibility()
        {
            var result = CircuitPanelValidation.Evaluate(null, null, null, null, null, null);
            Assert.True(result.CanAssign);
            Assert.Empty(result.Errors);
            Assert.Equal(4, result.Warnings.Count);
        }
        [Fact] public void EqualRatingsPassWithoutWarnings()
        {
            var result = CircuitPanelValidation.Evaluate(230, 230, 1, 1, 8000, 10000);
            Assert.True(result.CanAssign);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }
        [Fact] public void NonFiniteRatingsAreTreatedAsUnknown()
        {
            var result = CircuitPanelValidation.Evaluate(double.NaN, 230, 1, 1, 100, double.PositiveInfinity);
            Assert.True(result.CanAssign);
            Assert.Contains(result.Warnings, x => x.Contains("Voltage could not be verified"));
            Assert.Contains(result.Warnings, x => x.Contains("capacity was not checked"));
        }
    }
}
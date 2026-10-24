using System;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class BoxClearanceTests
    {
        [Fact]
        public void OverlappingBoxesHaveZeroDistance()
        {
            Assert.Equal(0.0, BoxClearance.Distance(0,0,0,2,2,2, 1,1,1,3,3,3));
        }

        [Fact]
        public void FaceSeparatedBoxesReturnGap()
        {
            Assert.Equal(2.0, BoxClearance.Distance(0,0,0,1,1,1, 3,0,0,4,1,1));
        }

        [Fact]
        public void DiagonalSeparationUsesEuclideanDistance()
        {
            Assert.Equal(Math.Sqrt(12), BoxClearance.Distance(0,0,0,1,1,1, 3,3,3,4,4,4), 10);
        }

        [Fact]
        public void RejectsReversedBounds()
        {
            Assert.Throws<ArgumentException>(() => BoxClearance.Distance(1,0,0,0,1,1, 2,2,2,3,3,3));
        }

        [Fact]
        public void RejectsNonFiniteCoordinates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoxClearance.Distance(double.NaN,0,0,1,1,1, 2,2,2,3,3,3));
        }
    }
}

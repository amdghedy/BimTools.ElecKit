using System.Collections.Generic;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class OrthogonalRoutePlannerTests
    {
        [Fact]
        public void DirectPathUsesTwoPointsWhenNoObstacleBlocksIt()
        {
            var path = OrthogonalRoutePlanner.Plan(0, 0, 10, 0, new RouteObstacle2D[0], 0);
            Assert.NotNull(path);
            Assert.Equal(2, path.Count);
            Assert.Equal(10, path[1].X - path[0].X);
        }
        [Fact]
        public void RoutesAroundObstacleAndPreservesOrthogonalSegments()
        {
            var path = OrthogonalRoutePlanner.Plan(0, 0, 10, 0,
                new[] { new RouteObstacle2D(4, -1, 6, 1) }, 0.5);
            Assert.NotNull(path);
            Assert.True(path.Count >= 4);
            for (int i = 1; i < path.Count; i++)
                Assert.True(path[i - 1].X == path[i].X || path[i - 1].Y == path[i].Y);
        }
        [Fact]
        public void RejectsEndpointInsideClearanceExpandedObstacle()
        {
            Assert.Null(OrthogonalRoutePlanner.Plan(1, 0, 10, 0,
                new[] { new RouteObstacle2D(0, -1, 2, 1) }, 0.25));
        }
        [Fact]
        public void RejectsInvalidClearance()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                OrthogonalRoutePlanner.Plan(0, 0, 1, 1, new List<RouteObstacle2D>(), -1));
        }
    }
}
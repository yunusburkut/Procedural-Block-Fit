using System.Collections.Generic;
using NUnit.Framework;
using Blokfit.Generation;

namespace Blokfit.Tests.EditMode
{
    // Renamed conceptually: now tests TrianglePartitioner (which replaced FloodFillPartitioner).
    // Triangle flat index: (row * n + col) * 2 + type   (type 0=lower, 1=upper)
    // Total triangles in n×n grid: n*n*2
    public class FloodFillPartitionerTests
    {
        [Test]
        public void CarveRegion_ReturnsExactTargetSize()
        {
            var rng = new System.Random(42);
            var partitioner = new TrianglePartitioner(4, rng);

            int[] region = partitioner.CarveRegion(0, 3);

            Assert.AreEqual(3, region.Length, "CarveRegion should return exactly targetSize triangles.");
        }

        [Test]
        public void CarveRegion_ReturnedCellsAreContiguous()
        {
            var rng = new System.Random(0);
            var partitioner = new TrianglePartitioner(5, rng);

            int[] region = partitioner.CarveRegion(0, 6);

            Assert.IsTrue(AreContiguous(region, 5), "All returned triangles should be adjacency-connected.");
        }

        [Test]
        public void FullPartition_CoversAllCells_4x4()
        {
            var rng = new System.Random(7);
            var partitioner = new TrianglePartitioner(4, rng);

            var covered = new HashSet<int>();
            while (partitioner.HasUnassigned(out int seed))
            {
                int[] region = partitioner.CarveRegion(seed, 4);
                foreach (int c in region) covered.Add(c);
            }

            Assert.AreEqual(32, covered.Count, "Every triangle in a 4x4 grid (32 total) should be covered exactly once.");
        }

        [Test]
        public void FullPartition_CoversAllCells_6x6()
        {
            var rng = new System.Random(99);
            var partitioner = new TrianglePartitioner(6, rng);

            var covered = new HashSet<int>();
            while (partitioner.HasUnassigned(out int seed))
            {
                int[] region = partitioner.CarveRegion(seed, 4);
                foreach (int c in region) covered.Add(c);
            }

            Assert.AreEqual(72, covered.Count, "Every triangle in a 6x6 grid (72 total) should be covered exactly once.");
        }

        [Test]
        public void CarveRegion_LargerThanRemaining_ReturnsAllRemaining()
        {
            var rng = new System.Random(1);
            var partitioner = new TrianglePartitioner(2, rng); // 2x2 = 8 triangles total

            partitioner.CarveRegion(0, 1); // carve 1
            int[] region = partitioner.CarveRegion(1, 100); // request more than remain

            Assert.AreEqual(7, region.Length, "CarveRegion should stop at remaining triangles when targetSize exceeds them.");
        }

        [Test]
        public void HasUnassigned_ReturnsFalseAfterFullCoverage()
        {
            var rng = new System.Random(5);
            var partitioner = new TrianglePartitioner(3, rng);

            while (partitioner.HasUnassigned(out int seed))
                partitioner.CarveRegion(seed, 99);

            bool hasMore = partitioner.HasUnassigned(out _);
            Assert.IsFalse(hasMore, "HasUnassigned should return false after all triangles are carved.");
        }

        // Verifies all triangles in the region are reachable from the first via triangle adjacency.
        private static bool AreContiguous(int[] flats, int n)
        {
            if (flats.Length <= 1) return true;

            var flatSet = new HashSet<int>(flats);
            var visited = new HashSet<int>();
            var stack   = new Stack<int>();
            stack.Push(flats[0]);
            visited.Add(flats[0]);

            while (stack.Count > 0)
            {
                int flat     = stack.Pop();
                int type     = flat % 2;
                int cellFlat = flat / 2;
                int col      = cellFlat % n;
                int row      = cellFlat / n;

                // Triangle adjacency mirrors TrianglePartitioner.GetFreeNeighbors
                var neighbors = new List<int>(3);
                if (type == 0)
                {
                    neighbors.Add((row * n + col) * 2 + 1);           // same cell upper
                    if (row > 0)     neighbors.Add(((row-1) * n + col)     * 2 + 1); // below
                    if (col < n - 1) neighbors.Add((row     * n + col + 1) * 2 + 1); // right
                }
                else
                {
                    neighbors.Add((row * n + col) * 2 + 0);           // same cell lower
                    if (row < n - 1) neighbors.Add(((row+1) * n + col)     * 2 + 0); // above
                    if (col > 0)     neighbors.Add((row     * n + col - 1) * 2 + 0); // left
                }

                foreach (int nb in neighbors)
                {
                    if (flatSet.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        stack.Push(nb);
                    }
                }
            }

            return visited.Count == flats.Length;
        }
    }
}

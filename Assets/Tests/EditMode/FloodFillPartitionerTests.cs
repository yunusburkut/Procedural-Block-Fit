using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Blokfit.Generation;

namespace Blokfit.Tests.EditMode
{
    public class FloodFillPartitionerTests
    {
        [Test]
        public void CarveRegion_ReturnsExactTargetSize()
        {
            var rng = new System.Random(42);
            var partitioner = new FloodFillPartitioner(4, rng);

            int[] region = partitioner.CarveRegion(0, 3);

            Assert.AreEqual(3, region.Length, "CarveRegion should return exactly targetSize cells.");
        }

        [Test]
        public void CarveRegion_ReturnedCellsAreContiguous()
        {
            var rng = new System.Random(0);
            var partitioner = new FloodFillPartitioner(5, rng);

            int[] region = partitioner.CarveRegion(12, 4);

            Assert.IsTrue(AreContiguous(region, 5), "All returned cells should be 4-connected.");
        }

        [Test]
        public void FullPartition_CoversAllCells_4x4()
        {
            var rng = new System.Random(7);
            var partitioner = new FloodFillPartitioner(4, rng);

            var covered = new HashSet<int>();
            while (partitioner.HasUnassigned(out int seed))
            {
                int[] region = partitioner.CarveRegion(seed, 4);
                foreach (int c in region) covered.Add(c);
            }

            Assert.AreEqual(16, covered.Count, "Every cell in a 4x4 grid should be covered exactly once.");
        }

        [Test]
        public void FullPartition_CoversAllCells_6x6()
        {
            var rng = new System.Random(99);
            var partitioner = new FloodFillPartitioner(6, rng);

            var covered = new HashSet<int>();
            while (partitioner.HasUnassigned(out int seed))
            {
                int[] region = partitioner.CarveRegion(seed, 3);
                foreach (int c in region) covered.Add(c);
            }

            Assert.AreEqual(36, covered.Count, "Every cell in a 6x6 grid should be covered exactly once.");
        }

        [Test]
        public void CarveRegion_LargerThanRemaining_ReturnsAllRemaining()
        {
            var rng = new System.Random(1);

            var partitioner = new FloodFillPartitioner(2, rng);
            partitioner.CarveRegion(0, 1);
            int[] region = partitioner.CarveRegion(1, 10);

            Assert.AreEqual(3, region.Length, "CarveRegion should stop at remaining cells when targetSize exceeds them.");
        }

        [Test]
        public void HasUnassigned_ReturnsFalseAfterFullCoverage()
        {
            var rng = new System.Random(5);
            var partitioner = new FloodFillPartitioner(3, rng);

            while (partitioner.HasUnassigned(out int seed))
                partitioner.CarveRegion(seed, 9);

            bool hasMore = partitioner.HasUnassigned(out _);
            Assert.IsFalse(hasMore, "HasUnassigned should return false after all cells are carved.");
        }

        private static bool AreContiguous(int[] cells, int n)
        {
            if (cells.Length <= 1) return true;

            var cellSet = new HashSet<int>(cells);
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(cells[0]);
            visited.Add(cells[0]);

            while (stack.Count > 0)
            {
                int flat = stack.Pop();
                int row = flat / n, col = flat % n;
                int[] neighbors =
                {
                    (row > 0)     ? (row - 1) * n + col : -1,
                    (row < n - 1) ? (row + 1) * n + col : -1,
                    (col > 0)     ? row * n + (col - 1) : -1,
                    (col < n - 1) ? row * n + (col + 1) : -1,
                };
                foreach (int nb in neighbors)
                {
                    if (nb >= 0 && cellSet.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        stack.Push(nb);
                    }
                }
            }

            return visited.Count == cells.Length;
        }
    }
}

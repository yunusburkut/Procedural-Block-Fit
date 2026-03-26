using System.Collections.Generic;

namespace Blokfit.Generation
{

    public class FloodFillPartitioner
    {
        private readonly bool[,] _assigned;
        private readonly int _n;
        private readonly System.Random _rng;

        public FloodFillPartitioner(int n, System.Random rng)
        {
            _n = n;
            _rng = rng;
            _assigned = new bool[n, n];
        }

        public int[] CarveRegion(int seedFlat, int targetSize)
        {
            int seedRow = seedFlat / _n;
            int seedCol = seedFlat % _n;

            _assigned[seedRow, seedCol] = true;

            var result = new List<int>(targetSize) { seedFlat };
            var queue = new Queue<int>();
            queue.Enqueue(seedFlat);

            while (queue.Count > 0 && result.Count < targetSize)
            {
                int current = queue.Dequeue();
                var neighbors = GetFreeNeighbors(current);
                Shuffle(neighbors);

                foreach (int neighbor in neighbors)
                {
                    if (result.Count >= targetSize) break;

                    int r = neighbor / _n;
                    int c = neighbor % _n;
                    _assigned[r, c] = true;
                    result.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return result.ToArray();
        }

        public bool HasUnassigned(out int seedFlat)
        {
            for (int r = 0; r < _n; r++)
            {
                for (int c = 0; c < _n; c++)
                {
                    if (!_assigned[r, c])
                    {
                        seedFlat = r * _n + c;
                        return true;
                    }
                }
            }
            seedFlat = -1;
            return false;
        }

        public void MarkAssigned(int[] cells)
        {
            foreach (int flat in cells)
                _assigned[flat / _n, flat % _n] = true;
        }

        private List<int> GetFreeNeighbors(int flat)
        {
            int row = flat / _n;
            int col = flat % _n;
            var neighbors = new List<int>(4);

            if (row > 0      && !_assigned[row - 1, col]) neighbors.Add((row - 1) * _n + col);
            if (row < _n - 1 && !_assigned[row + 1, col]) neighbors.Add((row + 1) * _n + col);
            if (col > 0      && !_assigned[row, col - 1]) neighbors.Add(row * _n + (col - 1));
            if (col < _n - 1 && !_assigned[row, col + 1]) neighbors.Add(row * _n + (col + 1));

            return neighbors;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

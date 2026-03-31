using System.Collections.Generic;

namespace Blokfit.Generation
{
    /// <summary>
    /// Flood-fill partitioner that works on a triangle grid of N×N×2 triangles.
    ///
    /// Triangle flat index: (row * N + col) * 2 + type
    ///   type 0 = lower  (/ diagonal: BL, BR, TR)
    ///   type 1 = upper  (/ diagonal: BL, TR, TL)
    ///
    /// Adjacency rules (each triangle has at most 3 neighbours):
    ///   lower(col,row)  ↔  upper(col,   row  )  via / diagonal
    ///   lower(col,row)  ↔  upper(col,   row-1)  via bottom edge
    ///   lower(col,row)  ↔  upper(col+1, row  )  via right  edge
    ///
    ///   upper(col,row)  ↔  lower(col,   row  )  via / diagonal
    ///   upper(col,row)  ↔  lower(col,   row+1)  via top    edge
    ///   upper(col,row)  ↔  lower(col-1, row  )  via left   edge
    /// </summary>
    public class TrianglePartitioner
    {
        private readonly bool[]        _assigned;          // true once a triangle is claimed by a region
        private readonly int           _n;                 // grid side length
        private readonly int           _total;             // n * n * 2
        private readonly System.Random _rng;
        private readonly List<int>     _neighborBuffer = new List<int>(3);   // reused to avoid per-call allocs

        public TrianglePartitioner(int n, System.Random rng)
        {
            _n        = n;
            _total    = n * n * 2;
            _rng      = rng;
            _assigned = new bool[_total];
        }

        /// <summary>
        /// BFS from <paramref name="seedFlat"/> claiming up to <paramref name="targetSize"/> unassigned triangles.
        /// Returns the actual array of claimed flat indices (may be smaller than targetSize near boundary).
        /// </summary>
        public int[] CarveRegion(int seedFlat, int targetSize)
        {
            _assigned[seedFlat] = true;
            var result = new List<int>(targetSize) { seedFlat };
            var queue  = new Queue<int>();
            queue.Enqueue(seedFlat);

            while (queue.Count > 0 && result.Count < targetSize)
            {
                int current   = queue.Dequeue();
                var neighbors = GetFreeNeighbors(current);
                Shuffle(neighbors);

                foreach (int neighbor in neighbors)
                {
                    if (result.Count >= targetSize) break;
                    _assigned[neighbor] = true;
                    result.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Finds the first unassigned triangle and returns its flat index via <paramref name="seedFlat"/>.
        /// Returns false when the entire grid is partitioned.
        /// </summary>
        public bool HasUnassigned(out int seedFlat)
        {
            for (int i = 0; i < _total; i++)
            {
                if (!_assigned[i]) { seedFlat = i; return true; }
            }
            seedFlat = -1;
            return false;
        }

        private List<int> GetFreeNeighbors(int flat)
        {
            int type     = flat % 2;
            int cellFlat = flat / 2;
            int col      = cellFlat % _n;
            int row      = cellFlat / _n;

            _neighborBuffer.Clear();

            if (type == 0) // lower → neighbours are all upper tris
            {
                AddIfFree(_neighborBuffer, col,     row,     1); // same cell, diagonal
                if (row > 0)     AddIfFree(_neighborBuffer, col,     row - 1, 1); // below
                if (col < _n-1)  AddIfFree(_neighborBuffer, col + 1, row,     1); // right
            }
            else           // upper → neighbours are all lower tris
            {
                AddIfFree(_neighborBuffer, col,     row,     0); // same cell, diagonal
                if (row < _n-1)  AddIfFree(_neighborBuffer, col,     row + 1, 0); // above
                if (col > 0)     AddIfFree(_neighborBuffer, col - 1, row,     0); // left
            }

            return _neighborBuffer;
        }

        private void AddIfFree(List<int> list, int col, int row, int type)
        {
            int flat = (row * _n + col) * 2 + type;
            if (!_assigned[flat]) list.Add(flat);
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

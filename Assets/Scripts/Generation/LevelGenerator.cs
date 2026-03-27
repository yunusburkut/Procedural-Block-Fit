using System;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Core;
using Blokfit.ScriptableObjects;

namespace Blokfit.Generation
{
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField] private float _trayStartX  = -5f;
        [SerializeField] private float _trayStartY  =  0f;
        [SerializeField] private float _traySpacing = 1.5f;

        public LevelData Generate(DifficultyConfig config, int seed = 0)
        {
            if (seed == 0) seed = Environment.TickCount;

            var rng = new System.Random(seed);
            int n   = config.gridSize;

            var partitioner = new TrianglePartitioner(n, rng);

            // Config sizes are in squares; multiply by 2 for triangles.
            int minTriSize = config.minPieceSize * 2;
            int maxTriSize = config.maxPieceSize * 2;

            var regions = new List<int[]>();
            while (partitioner.HasUnassigned(out int seedFlat))
            {
                int targetSize = rng.Next(minTriSize, maxTriSize + 1);
                int[] region   = partitioner.CarveRegion(seedFlat, targetSize);
                regions.Add(region);

                if (region.Length < minTriSize && regions.Count > 1)
                {
                    int[] nearest = FindNearestRegion(region, regions, n);
                    if (nearest != null)
                        MergeRegions(regions, region, nearest);
                }
            }

            while (regions.Count > config.maxPieces)
                MergeSmallestIntoNearest(regions, n);

            var pieces = new PieceJson[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                int[] cells  = regions[i];
                int   anchor = PickPrimaryAnchor(cells);
                Vector2 spawnPos = GetTraySpawnPosition(i, regions.Count);

                pieces[i] = new PieceJson
                {
                    id               = i,
                    cells            = cells,
                    color            = RandomColor(rng),
                    anchors          = new[] { anchor },
                    spawnX           = spawnPos.x,
                    spawnY           = spawnPos.y,
                    solutionRotation = 0f,
                };
            }

            return new LevelData
            {
                version    = 1,
                difficulty = config.difficultyName,
                grid       = new GridData { size = n },
                pieces     = pieces,
            };
        }

        // Anchor = the smallest triangle flat index in the piece (lowest row/col lower triangle).
        private static int PickPrimaryAnchor(int[] cells)
        {
            int best = cells[0];
            foreach (int c in cells)
                if (c < best) best = c;
            return best;
        }

        private Vector2 ComputeCentroid(int[] triCells, int n)
        {
            float sumX = 0, sumY = 0;
            foreach (int flat in triCells)
            {
                int cellFlat = flat / 2;
                sumX += cellFlat % n;
                sumY += cellFlat / n;
            }
            return new Vector2(sumX / triCells.Length, sumY / triCells.Length);
        }

        private int[] FindNearestRegion(int[] region, List<int[]> allRegions, int n)
        {
            int[]   best     = null;
            float   bestDist = float.MaxValue;
            Vector2 c        = ComputeCentroid(region, n);

            foreach (var other in allRegions)
            {
                if (other == region) continue;
                float d = Vector2.SqrMagnitude(c - ComputeCentroid(other, n));
                if (d < bestDist) { bestDist = d; best = other; }
            }
            return best;
        }

        private void MergeRegions(List<int[]> regions, int[] from, int[] into)
        {
            regions.Remove(from);
            int idx = regions.IndexOf(into);
            if (idx < 0) return;

            var merged = new int[into.Length + from.Length];
            into.CopyTo(merged, 0);
            from.CopyTo(merged, into.Length);
            regions[idx] = merged;
        }

        private void MergeSmallestIntoNearest(List<int[]> regions, int n)
        {
            int[] smallest = regions[0];
            foreach (var r in regions)
                if (r.Length < smallest.Length) smallest = r;

            int[] nearest = FindNearestRegion(smallest, regions, n);
            if (nearest != null)
                MergeRegions(regions, smallest, nearest);
        }

        private Vector2 GetTraySpawnPosition(int index, int total)
        {
            float y = _trayStartY + (index - total * 0.5f) * _traySpacing;
            return new Vector2(_trayStartX, y);
        }

        private static readonly string[] PaletteHex =
        {
            "#4A90D9", "#E94E77", "#50C878", "#F5A623",
            "#9B59B6", "#1ABC9C", "#E74C3C", "#3498DB",
            "#2ECC71", "#F39C12", "#8E44AD", "#16A085",
        };

        private string RandomColor(System.Random rng) =>
            PaletteHex[rng.Next(PaletteHex.Length)];
    }
}

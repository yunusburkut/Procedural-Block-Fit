using System;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Core;
using Blokfit.ScriptableObjects;

namespace Blokfit.Generation
{
    /// <summary>
    /// Procedurally generates a complete <see cref="LevelData"/> from a <see cref="DifficultyConfig"/>.
    /// Uses <see cref="TrianglePartitioner"/> to flood-fill the triangle grid into connected regions,
    /// then enforces piece-count constraints by merging small regions into neighbours.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField] private Transform _trayCenter;    // pivot for spawn position scatter
        [SerializeField] private float _traySpread = 1f;  // random offset radius

        /// <summary>
        /// Generates a full level layout. Pass <paramref name="seed"/> = 0 for a random seed.
        /// </summary>
        public LevelData Generate(DifficultyConfig config, int seed = 0)
        {
            if (seed == 0) seed = Environment.TickCount;

            var rng = new System.Random(seed);
            int n = config.gridSize;

            var partitioner = new TrianglePartitioner(n, rng);

            // Config sizes are in squares; multiply by 2 for triangles.
            int minTriSize = config.minPieceSize * 2;
            int maxTriSize = config.maxPieceSize * 2;

            var regions = new List<int[]>();
            while (partitioner.HasUnassigned(out int seedFlat))
            {
                int targetSize = rng.Next(minTriSize, maxTriSize + 1);
                int[] region = partitioner.CarveRegion(seedFlat, targetSize);
                regions.Add(region);

                if (region.Length < minTriSize && regions.Count > config.minPieces)
                {
                    int[] nearest = FindNearestRegion(region, regions, n);
                    if (nearest != null)
                        MergeRegions(regions, region, nearest);
                }
            }

            while (regions.Count > config.maxPieces)
                MergeSmallestIntoNearest(regions, n);

            // Shuffle palette once per level so each piece gets a unique colour.
            string[] palette = ShuffledPalette(rng);

            var pieces = new PieceJson[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                int[] cells = regions[i];
                int anchor = PickPrimaryAnchor(cells);
                Vector2 spawnPos = GetTraySpawnPosition(rng);

                pieces[i] = new PieceJson
                {
                    cells = cells,
                    color = palette[i % palette.Length],
                    anchors = new[] { anchor },
                    spawnX = spawnPos.x,
                    spawnY = spawnPos.y,
                };
            }

            return new LevelData
            {
                grid = new GridData { size = n },
                pieces = pieces,
            };
        }

        // Anchor = the smallest triangle flat index in the piece (lowest row/col lower triangle).
        private static int PickPrimaryAnchor(int[] cells)
        {
            int best = cells[0];
            foreach (int c in cells)
                if (c < best)
                    best = c;
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
            int[] best = null;
            float bestDist = float.MaxValue;
            Vector2 c = ComputeCentroid(region, n);

            foreach (var other in allRegions)
            {
                if (other == region) continue;
                float d = Vector2.SqrMagnitude(c - ComputeCentroid(other, n));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = other;
                }
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
                if (r.Length < smallest.Length)
                    smallest = r;

            int[] nearest = FindNearestRegion(smallest, regions, n);
            if (nearest != null)
                MergeRegions(regions, smallest, nearest);
        }

        private static readonly Vector2 DefaultTrayCenter = new Vector2(0f, -2.4f);

        /// <summary>Returns a random position near the tray center as the piece's initial spawn.</summary>
        private Vector2 GetTraySpawnPosition(System.Random rng)
        {
            Vector2 center = _trayCenter != null ? (Vector2)_trayCenter.position : DefaultTrayCenter;
            float ox = ((float)rng.NextDouble() * 2f - 1f) * _traySpread;
            float oy = ((float)rng.NextDouble() * 2f - 1f) * _traySpread;
            return center + new Vector2(ox, oy);
        }

        // 16 colours spread ~22° apart on the hue wheel — each is a clearly distinct hue.
        private static readonly string[] PaletteHex =
        {
            "#FF2222",   // red
            "#FF6600",   // orange-red
            "#FF9900",   // orange
            "#FFD700",   // golden yellow
            "#AADD00",   // yellow-green
            "#33CC33",   // green
            "#00BB77",   // spring green
            "#00CCCC",   // cyan
            "#0099DD",   // azure
            "#0055FF",   // blue
            "#5533FF",   // blue-violet
            "#AA00FF",   // violet
            "#DD00AA",   // magenta
            "#FF0066",   // rose
            "#FF66AA",   // light pink
            "#AA5500",   // brown
        };

        private static string[] ShuffledPalette(System.Random rng)
        {
            var palette = (string[])PaletteHex.Clone();
            for (int i = palette.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (palette[i], palette[j]) = (palette[j], palette[i]);
            }
            return palette;
        }
    }
}
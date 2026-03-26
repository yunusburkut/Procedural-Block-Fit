using System;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Core;
using Blokfit.ScriptableObjects;

namespace Blokfit.Generation
{

    public class LevelGenerator : MonoBehaviour
    {

        [SerializeField] private float _trayStartX = -5f;
        [SerializeField] private float _trayStartY =  0f;
        [SerializeField] private float _traySpacing = 1.5f;

        public LevelData Generate(DifficultyConfig config, int seed = 0)
        {
            if (seed == 0) seed = Environment.TickCount;

            var rng = new System.Random(seed);
            int n   = config.gridSize;

            var partitioner = new FloodFillPartitioner(n, rng);
            int targetPieceCount = rng.Next(config.minPieces, config.maxPieces + 1);

            var regions = new List<int[]>();
            while (partitioner.HasUnassigned(out int seedFlat))
            {
                int targetSize = rng.Next(config.minPieceSize, config.maxPieceSize + 1);
                int[] region   = partitioner.CarveRegion(seedFlat, targetSize);
                regions.Add(region);

                if (regions.Count > 0 && region.Length < config.minPieceSize && regions.Count > 1)
                {
                    int[] lastRegion = regions[regions.Count - 1];
                    int[] target     = FindNearestRegion(lastRegion, regions, n);
                    if (target != null)
                    {
                        MergeRegions(regions, lastRegion, target);
                    }
                }
            }

            while (regions.Count > config.maxPieces)
                MergeSmallestIntoNearest(regions, n, rng);

            var pieces = new PieceJson[regions.Count];
            for (int i = 0; i < regions.Count; i++)
            {
                int[] cells       = regions[i];
                int   anchorCount = rng.Next(config.minAnchors, config.maxAnchors + 1);
                int[] anchors     = PickAnchors(cells, anchorCount, n, rng);

                float solutionRot = 0f;
                if (config.allowRotations)
                {
                    int rotSteps = rng.Next(0, 4);
                    solutionRot  = rotSteps * 90f;
                }

                Vector2 spawnPos = GetTraySpawnPosition(i, regions.Count);

                pieces[i] = new PieceJson
                {
                    id              = i,
                    cells           = cells,
                    color           = RandomColor(rng),
                    anchors         = anchors,
                    spawnX          = spawnPos.x,
                    spawnY          = spawnPos.y,
                    solutionRotation = solutionRot,
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

        private int[] PickAnchors(int[] cells, int count, int n, System.Random rng)
        {

            Vector2 centroid = ComputeCentroid(cells, n);
            int     primary  = ClosestCell(cells, centroid, n);

            if (count <= 1 || cells.Length == 1)
                return new[] { primary };

            var perimeter = new List<int>();
            var cellSet   = new HashSet<int>(cells);
            foreach (int flat in cells)
            {
                if (flat == primary) continue;
                int row = flat / n, col = flat % n;
                int neighbours = 0;
                if (row > 0     && cellSet.Contains((row - 1) * n + col)) neighbours++;
                if (row < n - 1 && cellSet.Contains((row + 1) * n + col)) neighbours++;
                if (col > 0     && cellSet.Contains(row * n + col - 1))   neighbours++;
                if (col < n - 1 && cellSet.Contains(row * n + col + 1))   neighbours++;
                if (neighbours < 4) perimeter.Add(flat);
            }

            Shuffle(perimeter, rng);
            int extras = Math.Min(count - 1, perimeter.Count);

            var result = new int[1 + extras];
            result[0]  = primary;
            for (int i = 0; i < extras; i++)
                result[i + 1] = perimeter[i];

            return result;
        }

        private Vector2 ComputeCentroid(int[] cells, int n)
        {
            float sumX = 0, sumY = 0;
            foreach (int flat in cells)
            {
                sumX += flat % n;
                sumY += flat / n;
            }
            return new Vector2(sumX / cells.Length, sumY / cells.Length);
        }

        private int ClosestCell(int[] cells, Vector2 centroid, int n)
        {
            int   best     = cells[0];
            float bestDist = float.MaxValue;
            foreach (int flat in cells)
            {
                float d = Vector2.SqrMagnitude(centroid - new Vector2(flat % n, flat / n));
                if (d < bestDist) { bestDist = d; best = flat; }
            }
            return best;
        }

        private int[] FindNearestRegion(int[] region, List<int[]> allRegions, int n)
        {
            int[]  best     = null;
            float  bestDist = float.MaxValue;
            Vector2 c       = ComputeCentroid(region, n);

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

        private void MergeSmallestIntoNearest(List<int[]> regions, int n, System.Random rng)
        {

            int[]  smallest    = regions[0];
            foreach (var r in regions)
                if (r.Length < smallest.Length) smallest = r;

            int[]  nearest = FindNearestRegion(smallest, regions, n);
            if (nearest != null)
                MergeRegions(regions, smallest, nearest);
        }

        private Vector2 GetTraySpawnPosition(int index, int total)
        {
            float x = _trayStartX;
            float y = _trayStartY + (index - total * 0.5f) * _traySpacing;
            return new Vector2(x, y);
        }

        private static readonly string[] PaletteHex =
        {
            "#4A90D9", "#E94E77", "#50C878", "#F5A623",
            "#9B59B6", "#1ABC9C", "#E74C3C", "#3498DB",
            "#2ECC71", "#F39C12", "#8E44AD", "#16A085",
        };

        private string RandomColor(System.Random rng) =>
            PaletteHex[rng.Next(PaletteHex.Length)];

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

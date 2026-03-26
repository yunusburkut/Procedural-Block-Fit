using System;
using UnityEngine;

namespace Blokfit.Core
{
    [Serializable]
    public class LevelData
    {
        public int version;
        public string difficulty;
        public GridData grid;
        public PieceJson[] pieces;
    }

    [Serializable]
    public class GridData
    {
        public int size;
    }

    [Serializable]
    public class PieceJson
    {
        public int id;
        public int[] cells;
        public string color;
        public int[] anchors;
        public float spawnX;
        public float spawnY;
        public float solutionRotation;

        public Vector2 SpawnPosition => new Vector2(spawnX, spawnY);
    }
}

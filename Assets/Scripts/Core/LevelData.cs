using System;

namespace Blokfit.Core
{
    [Serializable]
    public class LevelData
    {
        public GridData    grid;
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
        public int[]  cells;
        public string color;
        public int[]  anchors;
        public float  spawnX;
        public float  spawnY;
    }
}

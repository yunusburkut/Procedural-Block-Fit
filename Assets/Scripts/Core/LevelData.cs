using System;

namespace Blokfit.Core
{
    /// <summary>Root JSON object for a single puzzle level.</summary>
    [Serializable]
    public class LevelData
    {
        public GridData    grid;
        public PieceJson[] pieces;
    }

    /// <summary>Board dimensions (N×N square grid, 2N² triangles total).</summary>
    [Serializable]
    public class GridData
    {
        public int size;   // number of cells per side
    }

    /// <summary>
    /// Serialised representation of one puzzle piece.
    /// <c>cells</c> stores triangle flat-indices: (row * N + col) * 2 + type.
    /// </summary>
    [Serializable]
    public class PieceJson
    {
        public int[]  cells;    // triangle flat indices that make up this piece
        public string color;    // HTML hex colour, e.g. "#4A90D9"
        public int[]  anchors;  // flat index of the primary anchor triangle
        public float  spawnX;   // world-space tray spawn position
        public float  spawnY;
    }
}

using UnityEngine;

namespace Blokfit.Pieces
{
    public struct PieceData
    {

        public int[] cells;

        public Vector2Int[] cellOffsets;

        public int[] anchorCells;

        public Color32 color;
        public int gridSize;

        public int primaryAnchorFlat;
    }
}

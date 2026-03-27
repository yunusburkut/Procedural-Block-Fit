using UnityEngine;

namespace Blokfit.Pieces
{
    /// <summary>
    /// Offset of one triangle from the piece's anchor vertex (bottom-left corner of anchor cell).
    /// Diagonal convention: / (bottom-left → top-right of each cell).
    ///   type 0 = lower triangle  (vertices: BL, BR, TR)
    ///   type 1 = upper triangle  (vertices: BL, TR, TL)
    /// </summary>
    public struct TriOffset
    {
        public int dcol;
        public int drow;
        public int type;
    }

    public struct PieceData
    {
        public int[]       cells;            // triangle flat indices: (row*gridSize+col)*2 + type
        public TriOffset[] triangleOffsets;  // per-triangle offset from anchor vertex
        public int[]       anchorCells;      // anchor triangle flat indices
        public Color32     color;
        public int         gridSize;
        public int         primaryAnchorFlat; // anchor triangle flat index
    }
}

using UnityEngine;

namespace Blokfit.Pieces
{
    /// <summary>
    /// Offset of one triangle from the piece's anchor vertex (bottom-left corner of anchor cell).
    /// Diagonal convention: / (bottom-left → top-right of each cell).
    ///   type 0 = lower triangle  (vertices: BL, BR, TR)
    ///   type 1 = upper triangle  (vertices: BL, TR, TL)
    /// </summary>
    public readonly struct TriOffset
    {
        public readonly int dcol;
        public readonly int drow;
        public readonly int type;

        public TriOffset(int dcol, int drow, int type)
        {
            this.dcol = dcol;
            this.drow = drow;
            this.type = type;
        }
    }

    public class PieceData
    {
        public int[]       Cells             { get; }
        public TriOffset[] TriangleOffsets   { get; }
        public int[]       AnchorCells       { get; }
        public Color32     Color             { get; }
        public int         GridSize          { get; }
        public int         PrimaryAnchorFlat { get; }

        public PieceData(int[] cells, TriOffset[] triangleOffsets, int[] anchorCells,
                         Color32 color, int gridSize, int primaryAnchorFlat)
        {
            Cells             = cells;
            TriangleOffsets   = triangleOffsets;
            AnchorCells       = anchorCells;
            Color             = color;
            GridSize          = gridSize;
            PrimaryAnchorFlat = primaryAnchorFlat;
        }
    }
}

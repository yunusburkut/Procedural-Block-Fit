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
        public readonly int dcol;   // column delta from anchor
        public readonly int drow;   // row delta from anchor
        public readonly int type;   // 0 = lower, 1 = upper

        public TriOffset(int dcol, int drow, int type)
        {
            this.dcol = dcol;
            this.drow = drow;
            this.type = type;
        }
    }

    /// <summary>
    /// Immutable runtime description of a single puzzle piece:
    /// the set of triangles it occupies (relative to its anchor) and its fill colour.
    /// </summary>
    public class PieceData
    {
        public TriOffset[] TriangleOffsets { get; }
        public Color32     Color           { get; }

        public PieceData(TriOffset[] triangleOffsets, Color32 color)
        {
            TriangleOffsets = triangleOffsets;
            Color           = color;
        }
    }
}

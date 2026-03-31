using Blokfit.Pieces;

namespace Blokfit.Input
{
    /// <summary>
    /// Immutable snapshot of an in-progress drag operation.
    /// Created on pointer-down and cleared on pointer-up.
    /// </summary>
    public class DragState
    {
        /// <summary>The piece currently being dragged.</summary>
        public PieceBehaviour Piece     { get; }
        /// <summary>Touch/pointer ID that started the drag (for multi-touch filtering).</summary>
        public int            PointerId { get; }

        public DragState(PieceBehaviour piece, int pointerId)
        {
            Piece     = piece;
            PointerId = pointerId;
        }
    }
}

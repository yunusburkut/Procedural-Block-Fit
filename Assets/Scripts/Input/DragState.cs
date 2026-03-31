using Blokfit.Pieces;

namespace Blokfit.Input
{
    public class DragState
    {
        public PieceBehaviour Piece     { get; }
        public int            PointerId { get; }

        public DragState(PieceBehaviour piece, int pointerId)
        {
            Piece     = piece;
            PointerId = pointerId;
        }
    }
}

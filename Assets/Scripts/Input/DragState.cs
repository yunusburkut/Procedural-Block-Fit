using UnityEngine;
using Blokfit.Pieces;

namespace Blokfit.Input
{

    public class DragState
    {
        public PieceBehaviour Piece          { get; }
        public int            PointerId      { get; }
        public Vector2        OriginalPosition { get; }
        public Vector2        PointerOffset  { get; }
        public DragState(PieceBehaviour piece, int pointerId, Vector2 originalPosition, Vector2 pointerOffset)
        {
            Piece            = piece;
            PointerId        = pointerId;
            OriginalPosition = originalPosition;
            PointerOffset    = pointerOffset;
        }
    }
}

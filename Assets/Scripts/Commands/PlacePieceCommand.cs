using UnityEngine;
using Blokfit.Board;
using Blokfit.Pieces;

namespace Blokfit.Commands
{
    /// <summary>
    /// Records a single piece placement so it can be undone.
    /// Execute: commits the piece to the board at <c>snapPoint</c>.
    /// Undo: lifts the piece off the board and returns it to its previous tray position.
    /// </summary>
    public class PlacePieceCommand : ICommand
    {
        private readonly PieceBehaviour _piece;
        private readonly BoardController _board;
        private readonly Vector2 _fromPosition;   // world position before the snap
        private readonly Vector2 _toPosition;     // world position after the snap
        private readonly SnapPoint _snapPoint;

        public PlacePieceCommand(
            PieceBehaviour piece,
            BoardController board,
            Vector2 fromPosition,
            Vector2 toPosition,
            SnapPoint snapPoint)
        {
            _piece        = piece;
            _board        = board;
            _fromPosition = fromPosition;
            _toPosition   = toPosition;
            _snapPoint    = snapPoint;
        }

        public void Execute()
        {
            if (_board.Place(_piece, _snapPoint))
                _piece.SetPlaced(_toPosition);
        }

        public void Undo()
        {
            _board.Lift(_piece);
            _piece.ReturnToTray(_fromPosition);
        }
    }
}

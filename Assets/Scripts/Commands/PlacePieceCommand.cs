using UnityEngine;
using Blokfit.Board;
using Blokfit.Pieces;

namespace Blokfit.Commands
{

    public class PlacePieceCommand : ICommand
    {
        private readonly PieceBehaviour _piece;
        private readonly BoardController _board;
        private readonly Vector2 _fromPosition;
        private readonly Vector2 _toPosition;
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
            _board.Place(_piece, _snapPoint);
            _piece.SetPlaced(_toPosition);
        }

        public void Undo()
        {
            _board.Lift(_piece);
            _piece.ReturnToTray(_fromPosition);
        }
    }
}

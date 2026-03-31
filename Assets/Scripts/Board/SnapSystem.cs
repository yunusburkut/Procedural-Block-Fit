using UnityEngine;
using Blokfit.Commands;
using Blokfit.Pieces;

namespace Blokfit.Board
{

    /// <summary>
    /// Handles snap-to-grid logic when a piece is released.
    /// Finds the nearest free snap point within the threshold distance, places the piece,
    /// and raises <see cref="OnMoveExecuted"/> so the command can be pushed onto the undo stack.
    /// </summary>
    public class SnapSystem : MonoBehaviour
    {
        [SerializeField] private BoardController _board;

        // Snap triggers when the anchor is within half a cell of a snap point
        private float _snapThreshold;
        private float _snapThresholdSq;   // pre-squared to avoid sqrt in hot path

        /// <summary>Raised after a successful snap; carries the reversible command.</summary>
        public event System.Action<ICommand> OnMoveExecuted;

        public void SetSnapThreshold(float cellSize)
        {
            _snapThreshold   = cellSize * 0.5f;
            _snapThresholdSq = _snapThreshold * _snapThreshold;
        }

        /// <summary>
        /// Attempts to snap <paramref name="piece"/> to the nearest free grid vertex.
        /// Returns true and fires <see cref="OnMoveExecuted"/> on success.
        /// </summary>
        public bool TrySnap(PieceBehaviour piece)
        {
            if (piece.AnchorTransforms == null || piece.AnchorTransforms.Count == 0)
                return false;

            Vector2 primaryAnchorWorldPos = piece.AnchorTransforms[0].position;

            SnapPoint candidate = _board.GetNearestFreeSnapPoint(primaryAnchorWorldPos);
            if (candidate == null) return false;

            // SqrMagnitude avoids a sqrt — compare squared distance against squared threshold.
            float distSq = Vector2.SqrMagnitude(primaryAnchorWorldPos - candidate.WorldPosition);
            if (distSq >= _snapThresholdSq) return false;

            Vector2 anchorLocalOffset = (Vector2)piece.AnchorTransforms[0].position - (Vector2)piece.transform.position;
            Vector2 targetPiecePos    = candidate.WorldPosition - anchorLocalOffset;
            Vector2 fromPos           = piece.transform.position;

            if (!_board.Place(piece, candidate)) return false;
            piece.SetPlaced(targetPiecePos);

            var cmd = new PlacePieceCommand(piece, _board, fromPos, targetPiecePos, candidate);
            OnMoveExecuted?.Invoke(cmd);
            return true;
        }
    }
}

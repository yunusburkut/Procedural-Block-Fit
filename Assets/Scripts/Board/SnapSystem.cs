using UnityEngine;
using Blokfit.Commands;
using Blokfit.Pieces;

namespace Blokfit.Board
{

    public class SnapSystem : MonoBehaviour
    {
        [SerializeField] private BoardController _board;

        private float _snapThreshold;

        public System.Action<ICommand> OnMoveExecuted;

        public void SetSnapThreshold(float cellSize)
        {
            _snapThreshold = cellSize * 0.5f;
        }

        public bool TrySnap(PieceBehaviour piece, Vector2 dropWorldPos)
        {
            if (piece.AnchorTransforms == null || piece.AnchorTransforms.Count == 0)
                return false;

            Vector2 primaryAnchorWorldPos = piece.AnchorTransforms[0].position;

            SnapPoint candidate = _board.GetNearestFreeSnapPoint(primaryAnchorWorldPos);
            if (candidate == null) return false;

            float dist = Vector2.Distance(primaryAnchorWorldPos, candidate.WorldPosition);
            if (dist >= _snapThreshold) return false;

            if (!_board.CanPlace(piece, candidate)) return false;

            Vector2 anchorLocalOffset = (Vector2)piece.AnchorTransforms[0].position - (Vector2)piece.transform.position;
            Vector2 targetPiecePos    = candidate.WorldPosition - anchorLocalOffset;

            var cmd = new PlacePieceCommand(piece, _board, piece.transform.position, targetPiecePos, candidate);
            cmd.Execute();
            OnMoveExecuted?.Invoke(cmd);
            return true;
        }
    }
}

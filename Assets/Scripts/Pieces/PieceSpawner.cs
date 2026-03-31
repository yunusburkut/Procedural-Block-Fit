using System.Collections.Generic;
using UnityEngine;
using Blokfit.Board;
using Blokfit.Core;
using Blokfit.Input;

namespace Blokfit.Pieces
{
    public class PieceSpawner : MonoBehaviour
    {
        [SerializeField] private PieceBehaviour  _piecePrefab;
        [SerializeField] private SnapSystem      _snapSystem;
        [SerializeField] private InputHandler    _inputHandler;
        [SerializeField] private BoardController _board;

        private readonly List<PieceBehaviour> _activePieces = new();

        public void SpawnAll(PieceJson[] pieceJsons, int gridSize)
        {
            for (int i = 0; i < pieceJsons.Length; i++)
            {
                var piece = SpawnOne(pieceJsons[i], gridSize);
                _activePieces.Add(piece);
                piece.AnimateIn(i);
            }
        }

        public void DestroyAll()
        {
            foreach (var piece in _activePieces)
            {
                if (piece != null)
                    Destroy(piece.gameObject);
            }
            _activePieces.Clear();
            PieceSortOrder.Reset();
        }

        private PieceBehaviour SpawnOne(PieceJson json, int gridSize)
        {
            PieceData data = PieceFactory.FromJson(json, gridSize);

            PieceBehaviour piece = Instantiate(_piecePrefab, transform);
            piece.transform.position = new Vector3(json.spawnX, json.spawnY, 0f);

            piece.Initialize(
                data,
                _board.CellSize,
                _snapSystem,
                _inputHandler,
                _board);

            return piece;
        }
    }
}

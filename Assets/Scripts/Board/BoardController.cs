using System;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Pieces;
using Blokfit.ScriptableObjects;

namespace Blokfit.Board
{

    public class BoardController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _boardRenderer;

        private Transform _snapPointRoot;

        public event Action OnBoardCompleted;

        private SnapPoint[,] _snapPoints;
        private bool[,] _occupiedCells;
        private int _gridSize;
        private float _cellSize;
        private float _boardWorldSize;
        private int _totalCells;
        private int _filledCells;

        private PieceBehaviour[,] _occupants;

        private MaterialPropertyBlock _boardMpb;
        private static readonly int GridSizeProp = Shader.PropertyToID("_GridSize");

        public int GridSize => _gridSize;
        public float CellSize => _cellSize;

        public void Initialize(int gridSize, float boardWorldSize)
        {
            _gridSize      = gridSize;
            _boardWorldSize = boardWorldSize;
            _cellSize      = boardWorldSize / gridSize;
            _totalCells    = gridSize * gridSize;
            _filledCells   = 0;

            RebuildSnapPointRoot();

            _snapPoints    = new SnapPoint[gridSize, gridSize];
            _occupiedCells = new bool[gridSize, gridSize];
            _occupants     = new PieceBehaviour[gridSize, gridSize];

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    Vector2 worldPos = GridToWorld(col, row);
                    var go = new GameObject();
                    go.transform.SetParent(_snapPointRoot, false);

                    var sp = go.AddComponent<SnapPoint>();
                    sp.Initialize(col, row, worldPos, _cellSize);
                    _snapPoints[row, col] = sp;
                }
            }

            SizeBoardQuad(boardWorldSize);
            UpdateBoardShader(gridSize);
        }

        public void ResetBoard()
        {
            if (_snapPoints == null) return;

            for (int r = 0; r < _gridSize; r++)
                for (int c = 0; c < _gridSize; c++)
                {
                    _occupiedCells[r, c] = false;
                    _occupants[r, c]     = null;
                    if (_snapPoints[r, c] != null)
                        _snapPoints[r, c].IsOccupied = false;
                }

            _filledCells = 0;
        }

        public SnapPoint GetNearestFreeSnapPoint(Vector2 worldPos)
        {
            SnapPoint best = null;
            float bestDist = float.MaxValue;

            for (int r = 0; r < _gridSize; r++)
            {
                for (int c = 0; c < _gridSize; c++)
                {
                    if (_occupiedCells[r, c]) continue;
                    float d = Vector2.SqrMagnitude(worldPos - _snapPoints[r, c].WorldPosition);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = _snapPoints[r, c];
                    }
                }
            }
            return best;
        }

        public bool CanPlace(PieceBehaviour piece, SnapPoint anchorSnap)
        {
            return GetTargetCells(piece, anchorSnap, out _);
        }

        public void Place(PieceBehaviour piece, SnapPoint anchorSnap)
        {
            if (!GetTargetCells(piece, anchorSnap, out var targets)) return;

            foreach ((int r, int c) in targets)
            {
                _occupiedCells[r, c] = true;
                _snapPoints[r, c].IsOccupied = true;
                _occupants[r, c] = piece;
            }

            _filledCells += targets.Count;
            CheckCompletion();
        }

        public void Lift(PieceBehaviour piece)
        {
            if (_occupiedCells == null) return;

            int freed = 0;
            for (int r = 0; r < _gridSize; r++)
            {
                for (int c = 0; c < _gridSize; c++)
                {
                    if (_occupants[r, c] == piece)
                    {
                        _occupiedCells[r, c] = false;
                        _snapPoints[r, c].IsOccupied = false;
                        _occupants[r, c] = null;
                        freed++;
                    }
                }
            }
            _filledCells -= freed;
        }
        
        public Vector2 GridToWorld(int col, int row)
        {
            float halfBoard = _boardWorldSize * 0.5f;
            float x = transform.position.x - halfBoard + _cellSize * col + _cellSize * 0.5f;
            float y = transform.position.y - halfBoard + _cellSize * row + _cellSize * 0.5f;
            return new Vector2(x, y);
        }

        private bool GetTargetCells(PieceBehaviour piece, SnapPoint anchorSnap, out List<(int r, int c)> targets)
        {
            targets = new List<(int, int)>(piece.Data.cells.Length);

            foreach (Vector2Int offset in piece.Data.cellOffsets)
            {
                int boardCol = anchorSnap.Col + offset.x;
                int boardRow = anchorSnap.Row + offset.y;

                if (boardCol < 0 || boardCol >= _gridSize || boardRow < 0 || boardRow >= _gridSize)
                    return false;

                if (_occupiedCells[boardRow, boardCol])
                    return false;

                targets.Add((boardRow, boardCol));
            }
            return true;
        }

        private void CheckCompletion()
        {
            if (_filledCells >= _totalCells)
                OnBoardCompleted?.Invoke();
        }

        private void RebuildSnapPointRoot()
        {

            if (_snapPointRoot != null)
                Destroy(_snapPointRoot.gameObject);

            var go = new GameObject("SnapPoints");
            go.transform.SetParent(transform, false);
            _snapPointRoot = go.transform;
        }

        private void SizeBoardQuad(float size)
        {

            if (_boardRenderer != null)
                _boardRenderer.transform.localScale = new Vector3(size, size, 1f);
        }

        private void UpdateBoardShader(int gridSize)
        {
            if (_boardRenderer == null) return;
            if (_boardMpb == null) _boardMpb = new MaterialPropertyBlock();
            _boardRenderer.GetPropertyBlock(_boardMpb);
            _boardMpb.SetFloat(GridSizeProp, gridSize);
            _boardRenderer.SetPropertyBlock(_boardMpb);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_snapPoints == null) return;

            for (int r = 0; r < _gridSize; r++)
            {
                for (int c = 0; c < _gridSize; c++)
                {
                    if (_snapPoints[r, c] == null) continue;
                    Gizmos.color = _occupiedCells[r, c] ? Color.red : Color.green;
                    Gizmos.DrawWireCube(_snapPoints[r, c].WorldPosition, Vector3.one * (_cellSize * 0.2f));
                }
            }
        }
#endif
    }
}

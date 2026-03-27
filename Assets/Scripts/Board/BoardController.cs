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

        // Snap points sit at every grid vertex: (gridSize+1)² points.
        private SnapPoint[,] _snapPoints;          // [vRow, vCol]

        // Occupancy tracked per triangle: [row, col, type]  (type 0=lower, 1=upper)
        private bool[,,]           _occupiedTriangles;
        private PieceBehaviour[,,] _occupants;

        private int   _gridSize;
        private float _cellSize;
        private float _boardWorldSize;
        private int   _totalTriangles;
        private int   _filledTriangles;

        private MaterialPropertyBlock _boardMpb;
        private static readonly int GridSizeProp = Shader.PropertyToID("_GridSize");

        public int   GridSize => _gridSize;
        public float CellSize => _cellSize;

        public void Initialize(int gridSize, float boardWorldSize)
        {
            _gridSize        = gridSize;
            _boardWorldSize  = boardWorldSize;
            _cellSize        = boardWorldSize / gridSize;
            _totalTriangles  = gridSize * gridSize * 2;
            _filledTriangles = 0;

            RebuildSnapPointRoot();

            int vn = gridSize + 1;
            _snapPoints        = new SnapPoint[vn, vn];
            _occupiedTriangles = new bool[gridSize, gridSize, 2];
            _occupants         = new PieceBehaviour[gridSize, gridSize, 2];

            for (int vRow = 0; vRow <= gridSize; vRow++)
            {
                for (int vCol = 0; vCol <= gridSize; vCol++)
                {
                    Vector2 worldPos = GridVertexToWorld(vCol, vRow);
                    var go = new GameObject();
                    go.transform.SetParent(_snapPointRoot, false);

                    var sp = go.AddComponent<SnapPoint>();
                    sp.Initialize(vCol, vRow, worldPos, _cellSize);
                    _snapPoints[vRow, vCol] = sp;
                }
            }

            SizeBoardQuad(boardWorldSize);
            UpdateBoardShader(gridSize);
        }

        public void ResetBoard()
        {
            if (_occupiedTriangles == null) return;

            for (int r = 0; r < _gridSize; r++)
                for (int c = 0; c < _gridSize; c++)
                    for (int t = 0; t < 2; t++)
                    {
                        _occupiedTriangles[r, c, t] = false;
                        _occupants[r, c, t]         = null;
                    }

            _filledTriangles = 0;
        }

        public SnapPoint GetNearestFreeSnapPoint(Vector2 worldPos)
        {
            SnapPoint best     = null;
            float     bestDist = float.MaxValue;

            for (int vRow = 0; vRow <= _gridSize; vRow++)
            {
                for (int vCol = 0; vCol <= _gridSize; vCol++)
                {
                    float d = Vector2.SqrMagnitude(worldPos - _snapPoints[vRow, vCol].WorldPosition);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best     = _snapPoints[vRow, vCol];
                    }
                }
            }
            return best;
        }

        public bool CanPlace(PieceBehaviour piece, SnapPoint anchorVertex)
        {
            return GetTargetTriangles(piece, anchorVertex, out _);
        }

        public void Place(PieceBehaviour piece, SnapPoint anchorVertex)
        {
            if (!GetTargetTriangles(piece, anchorVertex, out var targets)) return;

            foreach ((int r, int c, int t) in targets)
            {
                _occupiedTriangles[r, c, t] = true;
                _occupants[r, c, t]         = piece;
            }

            _filledTriangles += targets.Count;
            CheckCompletion();
        }

        public void Lift(PieceBehaviour piece)
        {
            if (_occupiedTriangles == null) return;

            int freed = 0;
            for (int r = 0; r < _gridSize; r++)
                for (int c = 0; c < _gridSize; c++)
                    for (int t = 0; t < 2; t++)
                        if (_occupants[r, c, t] == piece)
                        {
                            _occupiedTriangles[r, c, t] = false;
                            _occupants[r, c, t]         = null;
                            freed++;
                        }

            _filledTriangles -= freed;
        }

        /// <summary>World position of grid vertex (vCol, vRow).</summary>
        public Vector2 GridVertexToWorld(int vCol, int vRow)
        {
            float halfBoard = _boardWorldSize * 0.5f;
            float x = transform.position.x - halfBoard + _cellSize * vCol;
            float y = transform.position.y - halfBoard + _cellSize * vRow;
            return new Vector2(x, y);
        }

        private bool GetTargetTriangles(
            PieceBehaviour piece,
            SnapPoint      anchorVertex,
            out List<(int r, int c, int t)> targets)
        {
            targets = new List<(int, int, int)>(piece.Data.triangleOffsets.Length);

            foreach (var tri in piece.Data.triangleOffsets)
            {
                int boardCol = anchorVertex.Col + tri.dcol;
                int boardRow = anchorVertex.Row + tri.drow;
                int type     = tri.type;

                if (boardCol < 0 || boardCol >= _gridSize ||
                    boardRow < 0 || boardRow >= _gridSize)
                    return false;

                if (_occupiedTriangles[boardRow, boardCol, type])
                    return false;

                targets.Add((boardRow, boardCol, type));
            }
            return true;
        }

        private void CheckCompletion()
        {
            if (_filledTriangles >= _totalTriangles)
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

            for (int vRow = 0; vRow <= _gridSize; vRow++)
            {
                for (int vCol = 0; vCol <= _gridSize; vCol++)
                {
                    if (_snapPoints[vRow, vCol] == null) continue;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(_snapPoints[vRow, vCol].WorldPosition, _cellSize * 0.08f);
                }
            }
        }
#endif
    }
}

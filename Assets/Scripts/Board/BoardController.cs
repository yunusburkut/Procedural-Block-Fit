using System;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Pieces;

namespace Blokfit.Board
{
    /// <summary>
    /// Owns the board state: snap-point grid, triangle occupancy map, and completion detection.
    /// Exposes <see cref="Place"/> / <see cref="Lift"/> for reversible piece placement,
    /// and raises <see cref="OnBoardCompleted"/> when every triangle is filled.
    /// </summary>
    public class BoardController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _boardRenderer;
        [SerializeField] private Rect         _boardRect = new Rect(-3.5f, -3.5f, 7f, 7f);

        private Transform _snapPointRoot;

        /// <summary>Fired once when all triangles on the board are occupied.</summary>
        public event Action OnBoardCompleted;

        // Snap points sit at every grid vertex: (gridSize+1)² points.
        private SnapPoint[,] _snapPoints;          // [vRow, vCol]

        // Occupancy tracked per triangle: [row, col, type]  (type 0=lower, 1=upper)
        private bool[,,]           _occupiedTriangles;
        private PieceBehaviour[,,] _occupants;

        // Reverse lookup: piece → list of triangles it occupies (enables O(1) Lift)
        private readonly Dictionary<PieceBehaviour, List<(int r, int c, int t)>> _pieceOccupancy = new();

        private int   _gridSize;
        private float _cellSize;
        private float _boardWorldSize;
        private int   _totalTriangles;    // gridSize² * 2
        private int   _filledTriangles;   // incremented on Place, decremented on Lift

        private MaterialPropertyBlock _boardMpb;
        private static readonly int GridSizeProp  = Shader.PropertyToID("_GridSize");
        private static readonly int LineScaleProp = Shader.PropertyToID("_LineScale");

        public int   GridSize => _gridSize;
        public float CellSize => _cellSize;

        // Pre-computed board origin (world position of vertex [0,0]) — avoids per-call arithmetic.
        private Vector2 _boardOrigin;

        /// <summary>Initialises snap points, occupancy arrays and board shader for a given grid size.</summary>
        public void Initialize(int gridSize)
        {
            float size = Mathf.Min(_boardRect.width, _boardRect.height);
            transform.position = new Vector3(_boardRect.center.x, _boardRect.center.y, transform.position.z);

            _gridSize        = gridSize;
            _boardWorldSize  = size;
            _cellSize        = size / gridSize;
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
                    sp.Initialize(vCol, vRow, worldPos, _cellSize, gridSize);
                    _snapPoints[vRow, vCol] = sp;
                }
            }

            SizeBoardQuad(size);
            UpdateBoardShader(gridSize);

            // Cache origin once so GetNearestFreeSnapPoint does zero extra arithmetic.
            _boardOrigin = new Vector2(
                transform.position.x - _boardWorldSize * 0.5f,
                transform.position.y - _boardWorldSize * 0.5f);
        }

        /// <summary>Clears all occupancy data; called between levels.</summary>
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
            _pieceOccupancy.Clear();
        }

        /// <summary>
        /// Returns the snap point whose grid vertex is nearest to <paramref name="worldPos"/>.
        /// O(1): rounds the world position to the nearest integer grid coordinate.
        /// </summary>
        public SnapPoint GetNearestFreeSnapPoint(Vector2 worldPos)
        {
            // Convert world position to fractional grid coordinates, then round.
            int vCol = Mathf.Clamp(Mathf.RoundToInt((worldPos.x - _boardOrigin.x) / _cellSize), 0, _gridSize);
            int vRow = Mathf.Clamp(Mathf.RoundToInt((worldPos.y - _boardOrigin.y) / _cellSize), 0, _gridSize);
            return _snapPoints[vRow, vCol];
        }

        /// <summary>
        /// Tries to place <paramref name="piece"/> with its anchor at <paramref name="anchorVertex"/>.
        /// Fails if any required triangle is out-of-bounds or already occupied.
        /// </summary>
        public bool Place(PieceBehaviour piece, SnapPoint anchorVertex)
        {
            if (!GetTargetTriangles(piece, anchorVertex, out var targets)) return false;

            foreach ((int r, int c, int t) in targets)
            {
                _occupiedTriangles[r, c, t] = true;
                _occupants[r, c, t]         = piece;
            }

            _pieceOccupancy[piece] = targets;
            _filledTriangles += targets.Count;
            CheckCompletion();
            return true;
        }

        /// <summary>Removes <paramref name="piece"/> from the board so it can be dragged again.</summary>
        public void Lift(PieceBehaviour piece)
        {
            if (!_pieceOccupancy.TryGetValue(piece, out var cells)) return;

            foreach ((int r, int c, int t) in cells)
            {
                _occupiedTriangles[r, c, t] = false;
                _occupants[r, c, t]         = null;
            }

            _filledTriangles -= cells.Count;
            _pieceOccupancy.Remove(piece);
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
            targets = new List<(int, int, int)>(piece.Data.TriangleOffsets.Length);

            foreach (var tri in piece.Data.TriangleOffsets)
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
            if (_boardRenderer == null) return;
            float lineScale = _boardRenderer.sharedMaterial != null
                ? _boardRenderer.sharedMaterial.GetFloat(LineScaleProp)
                : 0.5f;
            float expanded = size + _cellSize * lineScale * 2f;
            _boardRenderer.transform.localScale = new Vector3(expanded, expanded, 1f);
            _boardRenderer.sortingOrder = -10;
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

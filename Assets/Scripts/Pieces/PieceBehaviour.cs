using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using Blokfit.Board;
using Blokfit.Input;

namespace Blokfit.Pieces
{
    /// <summary>
    /// MonoBehaviour that drives a single puzzle piece:
    /// renders per-triangle sprites, manages the polygon collider, handles pointer events,
    /// and delegates snap/board placement to <see cref="SnapSystem"/> and <see cref="BoardController"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(PolygonCollider2D))]
    public class PieceBehaviour : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        public PieceData Data { get; private set; }
        public List<Transform> AnchorTransforms { get; private set; }

        private SnapSystem        _snapSystem;
        private InputHandler      _inputHandler;
        private BoardController   _board;

        private PolygonCollider2D _collider;

        private Vector2 _dragOffset;
        private float   _cellSize;
        private Camera  _camera;
        private float   _cameraZ;   // cached once — camera is fixed in this game

        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private const int   DragSortBoost   = 1000;
        private const float DragScale       = 1.1f;
        // Animation constants
        private const float AnimInDuration  = 0.55f;
        private const float AnimInStagger   = 0.12f;   // delay per piece index
        private const float AnimInStartY    = 8f;      // off-screen Y start
        private const float SnapDuration    = 0.15f;
        private const float ReturnDuration  = 0.2f;

        private int _myOrder;

        private Vector3 _screenToWorldBuffer;   // reused every drag frame — avoids per-frame heap alloc

        private readonly List<(int col, int row)> _snapCandidateBuffer = new List<(int col, int row)>();
        private static readonly int[] _snapThresholds = { 6, 4, 2, 0 };

        // Shared triangle sprites (created once, reused by every piece instance)
        private static Sprite _lowerTriSprite;   // type 0: BL-BR-TR
        private static Sprite _upperTriSprite;   // type 1: BL-TR-TL

        private List<SpriteRenderer> _triRenderers;
        private List<SpriteRenderer> _snapDots;    // random vertex markers, visible only while dragging

        /// <summary>Wires up all dependencies and builds sprites, collider, and anchor transforms.</summary>
        public void Initialize(
            PieceData         data,
            float             cellSize,
            SnapSystem        snapSystem,
            InputHandler      inputHandler,
            BoardController   board)
        {
            Data          = data;
            _cellSize     = cellSize;
            _snapSystem   = snapSystem;
            _inputHandler = inputHandler;
            _board        = board;

            _collider = GetComponent<PolygonCollider2D>();
            _camera   = Camera.main;
            _cameraZ  = -_camera.transform.position.z;   // cache: camera never moves
            _myOrder  = PieceSortOrder.Next();

            BuildTriangleSprites();
            BuildCollider();
            BuildAnchorTransforms();
            BuildSnapDots();
        }

        /// <summary>Animates the piece dropping in from above with a staggered delay.</summary>
        public void AnimateIn(int index, float startY = AnimInStartY)
        {
            Vector3 target = transform.position;
            transform.position = new Vector3(target.x, startY, target.z);

            float delay = index * AnimInStagger;
            transform.DOMove(target, AnimInDuration)
                .SetDelay(delay)
                .SetEase(Ease.OutBounce);
        }

        /// <summary>Tweens the piece to its snapped world position and marks it as placed.</summary>
        public void SetPlaced(Vector2 snapWorldPos)
        {
            SetDragging(false);
            transform.DOKill();
            transform.DOMove(snapWorldPos, SnapDuration).SetEase(Ease.OutQuad);
        }

        /// <summary>Tweens the piece back to <paramref name="originalPos"/> when a snap fails.</summary>
        /// <param name="originalPos">Position captured at drag-start time (not necessarily the tray spawn position).</param>
        public void ReturnToTray(Vector2 originalPos)
        {
            SetDragging(false);
            transform.DOKill();
            transform.DOMove(originalPos, ReturnDuration).SetEase(Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_inputHandler.BeginDrag(this, eventData))
                return;

            Vector2 worldPoint = ScreenToWorld(eventData.position);
            _dragOffset = (Vector2)transform.position - worldPoint;

            _myOrder = PieceSortOrder.Next();
            _board.Lift(this);
            SetDragging(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_inputHandler.CurrentDrag?.Piece != this) return;
            Vector2 worldPoint = ScreenToWorld(eventData.position);
            transform.position = worldPoint + _dragOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_inputHandler.CurrentDrag?.Piece != this) return;
            _ = _snapSystem.TrySnap(this);
            SetDragging(false);
            _inputHandler.EndDrag();
        }

        private void SetDragging(bool dragging)
        {
            transform.localScale = dragging ? Vector3.one * DragScale : Vector3.one;

            int order = _myOrder + (dragging ? DragSortBoost : 0);

            if (_triRenderers != null)
                foreach (var sr in _triRenderers)
                    sr.sortingOrder = order;

            if (_snapDots != null)
                foreach (var dot in _snapDots)
                {
                    // Always 1 above the piece triangles regardless of drag state.
                    dot.sortingOrder = order + 1;

                    dot.DOKill();
                    dot.DOFade(dragging ? 0.9f : 0f, dragging ? 0.15f : 0.12f)
                       .SetEase(dragging ? Ease.OutQuad : Ease.InQuad);
                }
        }

        // ── Build ──────────────────────────────────────────────────────────

        private void BuildTriangleSprites()
        {
            GetComponent<SpriteRenderer>().enabled = false;

            if (_lowerTriSprite == null) _lowerTriSprite = CreateTriSprite(0);
            if (_upperTriSprite == null) _upperTriSprite = CreateTriSprite(1);

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ColorProp, (Color)Data.Color);

            _triRenderers = new List<SpriteRenderer>(Data.TriangleOffsets.Length);

            foreach (var tri in Data.TriangleOffsets)
            {
                var cellGo = new GameObject($"Tri_{tri.dcol}_{tri.drow}_{tri.type}");
                cellGo.transform.SetParent(transform, false);
                cellGo.transform.localPosition = new Vector3(tri.dcol * _cellSize, tri.drow * _cellSize, 0f);
                cellGo.transform.localScale    = Vector3.one * _cellSize;

                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite       = tri.type == 0 ? _lowerTriSprite : _upperTriSprite;
                sr.sortingOrder = _myOrder;
                sr.SetPropertyBlock(mpb);

                _triRenderers.Add(sr);
            }
        }

        private void BuildCollider()
        {
            _collider.pathCount = Data.TriangleOffsets.Length;

            for (int i = 0; i < Data.TriangleOffsets.Length; i++)
            {
                var tri = Data.TriangleOffsets[i];
                float x0 = tri.dcol       * _cellSize;
                float y0 = tri.drow       * _cellSize;
                float x1 = (tri.dcol + 1) * _cellSize;
                float y1 = (tri.drow + 1) * _cellSize;

                Vector2[] path = tri.type == 0
                    ? new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1) }
                    : new[] { new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

                _collider.SetPath(i, path);
            }
        }

        private void BuildAnchorTransforms()
        {
            // The anchor vertex is always at local (0,0) – the piece pivot IS the anchor.
            AnchorTransforms = new List<Transform>(1) { transform };
        }

        /// <summary>
        /// Places snap-dot visuals at vertices that are "interior" to the piece shape:
        /// only where 2 or more of the piece's own triangles meet.
        /// Outer tips (touched by a single triangle) are excluded.
        /// Falls back to all vertices if no interior ones exist (tiny pieces).
        /// </summary>
        private void BuildSnapDots()
        {
            // Fast lookup: which (dcol, drow, type) triangles belong to this piece.
            var triSet = new HashSet<(int dc, int dr, int t)>();
            foreach (var tri in Data.TriangleOffsets)
                triSet.Add((tri.dcol, tri.drow, tri.type));

            // Collect every unique vertex and its surrounding-triangle count.
            var allVerts  = new HashSet<(int col, int row)>();
            var vertCount = new Dictionary<(int col, int row), int>();

            foreach (var tri in Data.TriangleOffsets)
            {
                (int col, int row)[] corners = tri.type == 0
                    ? new[] { (tri.dcol, tri.drow), (tri.dcol + 1, tri.drow), (tri.dcol + 1, tri.drow + 1) }
                    : new[] { (tri.dcol, tri.drow), (tri.dcol + 1, tri.drow + 1), (tri.dcol, tri.drow + 1) };

                foreach (var v in corners)
                {
                    if (!allVerts.Add(v)) continue;
                    vertCount[v] = CountSurrounding(triSet, v.col, v.row);
                }
            }

            // Step-down: use the most deeply interior vertices available.
            // count==6 → fully enclosed (all 6 surrounding tris belong to piece) — never on boundary
            // count>=4 → mostly enclosed
            // count>=2 → shared edge (piece outer boundary — allowed only if no better option)
            // fallback  → any vertex (single-triangle pieces)
            _snapCandidateBuffer.Clear();
            List<(int col, int row)> candidates = null;
            foreach (int threshold in _snapThresholds)
            {
                _snapCandidateBuffer.Clear();
                foreach (var kv in vertCount)
                    if (kv.Value >= threshold)
                        _snapCandidateBuffer.Add(kv.Key);
                if (_snapCandidateBuffer.Count > 0) { candidates = _snapCandidateBuffer; break; }
            }

            // Fisher-Yates shuffle for a random selection.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            // Weighted roll: ~80 % → 1,  ~12 % → 2,  ~5 % → 3,  ~3 % → 4.
            float roll = UnityEngine.Random.value;
            int dotCount;
            if      (roll < 0.80f) dotCount = 1;
            else if (roll < 0.92f) dotCount = 2;
            else if (roll < 0.97f) dotCount = 3;
            else                   dotCount = 4;
            dotCount = Mathf.Clamp(dotCount, 1, candidates.Count);

            _snapDots = new List<SpriteRenderer>(dotCount);
            var circle = CreateCircleSprite();

            for (int i = 0; i < dotCount; i++)
            {
                (int col, int row) = candidates[i];

                var go = new GameObject($"SnapDot_{col}_{row}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(col * _cellSize, row * _cellSize, 0f);
                go.transform.localScale    = Vector3.one * (_cellSize * 0.32f);

                var sr          = go.AddComponent<SpriteRenderer>();
                sr.sprite       = circle;
                sr.sortingOrder = _myOrder + DragSortBoost + 1;  // always above piece triangles
                sr.color        = new Color(1f, 1f, 1f, 0f);     // start invisible

                _snapDots.Add(sr);
            }
        }

        /// <summary>
        /// Counts how many of the 6 triangles surrounding vertex (vCol, vRow) belong to this piece.
        /// Each vertex in a triangular half-cell grid can be shared by up to 6 triangles:
        /// 3 lower-type (type=0) and 3 upper-type (type=1).
        /// </summary>
        private static int CountSurrounding(HashSet<(int dc, int dr, int t)> triSet, int vCol, int vRow)
        {
            int n = 0;
            // Lower triangles (type 0): BL=(col,row) BR=(col+1,row) TR=(col+1,row+1)
            if (triSet.Contains((vCol,     vRow,     0))) n++;  // BL of this lower tri
            if (triSet.Contains((vCol - 1, vRow,     0))) n++;  // BR of lower tri to the left
            if (triSet.Contains((vCol - 1, vRow - 1, 0))) n++;  // TR of lower tri below-left
            // Upper triangles (type 1): BL=(col,row) TR=(col+1,row+1) TL=(col,row+1)
            if (triSet.Contains((vCol,     vRow,     1))) n++;  // BL of this upper tri
            if (triSet.Contains((vCol - 1, vRow - 1, 1))) n++;  // TR of upper tri below-left
            if (triSet.Contains((vCol,     vRow - 1, 1))) n++;  // TL of upper tri below
            return n;
        }

        /// <summary>Creates a soft white circle sprite (64×64 px, pivot at centre).</summary>
        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };

            float ctr    = (size - 1) * 0.5f;
            float radius = ctr * 0.70f;
            var   px     = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist  = Mathf.Sqrt((x - ctr) * (x - ctr) + (y - ctr) * (y - ctr));
                float alpha = Mathf.Clamp01(radius - dist + 1f);  // +1 px soft edge
                px[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ── Sprite factory ────────────────────────────────────────────────

        /// <summary>
        /// Creates a 64×64 white sprite shaped as a right-angled triangle.
        /// type 0 = lower (/ diagonal, occupies lower-right half of cell)
        /// type 1 = upper (/ diagonal, occupies upper-left  half of cell)
        /// Pivot at bottom-left (0,0) so localPosition = cell BL in parent space.
        /// </summary>
        private static Sprite CreateTriSprite(int type)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge  = type == 0 ? (x - y) : (y - x);
                    float alpha = Mathf.Clamp01(edge + 1.0f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0f), size);
        }

        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            // _cameraZ is cached at Initialize; avoids a Transform.position access every drag frame.
            // _screenToWorldBuffer is reused to avoid per-frame heap allocation.
            _screenToWorldBuffer.x = screenPos.x;
            _screenToWorldBuffer.y = screenPos.y;
            _screenToWorldBuffer.z = _cameraZ;
            return _camera.ScreenToWorldPoint(_screenToWorldBuffer);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (AnchorTransforms == null) return;
            Gizmos.color = Color.yellow;
            float s = _cellSize * 0.15f;
            foreach (var t in AnchorTransforms)
            {
                Vector3 p = t.position;
                Gizmos.DrawLine(p + new Vector3(-s, 0, 0), p + new Vector3(s, 0, 0));
                Gizmos.DrawLine(p + new Vector3(0, -s, 0), p + new Vector3(0, s, 0));
            }
        }
#endif
    }
}

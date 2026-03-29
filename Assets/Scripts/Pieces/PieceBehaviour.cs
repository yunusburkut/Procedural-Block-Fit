using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Blokfit.Board;
using Blokfit.Input;
using Blokfit.ScriptableObjects;

namespace Blokfit.Pieces
{
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
        private DifficultyConfig  _config;

        private SpriteRenderer    _spriteRenderer;
        private PolygonCollider2D _collider;

        private Vector2 _originalPosition;
        private Vector2 _dragOffset;
        private float   _cellSize;
        private Camera  _camera;

        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private const int   DragSortBoost = 1000;
        private const float DragScale     = 1.1f;

        private static int _globalSortCounter = 0;
        private int _myOrder = 0;

        public static void ResetSortCounter() => _globalSortCounter = 0;

        // Shared triangle sprites (lower = type 0, upper = type 1)
        private static Sprite _lowerTriSprite;
        private static Sprite _upperTriSprite;

        private List<SpriteRenderer> _triRenderers;

        public void Initialize(
            PieceData         data,
            float             cellSize,
            DifficultyConfig  config,
            SnapSystem        snapSystem,
            InputHandler      inputHandler,
            BoardController   board)
        {
            Data          = data;
            _cellSize     = cellSize;
            _config       = config;
            _snapSystem   = snapSystem;
            _inputHandler = inputHandler;
            _board        = board;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider       = GetComponent<PolygonCollider2D>();

            _originalPosition = transform.position;
            _camera           = Camera.main;

            BuildTriangleSprites();
            BuildCollider();
            BuildAnchorTransforms();
        }

        public void SetPlaced(Vector2 snapWorldPos)
        {
            transform.position = snapWorldPos;
            SetDragging(false);
        }

        public void ReturnToTray(Vector2 originalPos)
        {
            transform.position = originalPos;
            SetDragging(false);
        }

        // Visual rotation only – triangle offsets are not updated.
        public void ApplyRotation(float degrees)
        {
            transform.Rotate(0f, 0f, degrees);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_inputHandler.IsDragging && _inputHandler.CurrentDrag.Piece == this
                && eventData.pointerId != _inputHandler.CurrentDrag.PointerId)
            {
                if (_config.allowRotations)
                    ApplyRotation(90f);
                return;
            }

            Vector2 worldPoint = ScreenToWorld(eventData.position);
            Vector2 offset     = (Vector2)transform.position - worldPoint;

            if (!_inputHandler.BeginDrag(this, eventData, offset))
                return;

            _myOrder = ++_globalSortCounter;
            _board.Lift(this);
            SetDragging(true);
            _dragOffset = offset;
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
            _snapSystem.TrySnap(this);
            SetDragging(false);
            _inputHandler.EndDrag();
        }

        private void SetDragging(bool dragging)
        {
            transform.localScale = dragging ? Vector3.one * DragScale : Vector3.one;

            int order = _myOrder + (dragging ? DragSortBoost : 0);
            if (_triRenderers == null) return;
            foreach (var sr in _triRenderers)
                sr.sortingOrder = order;
        }

        // ── Build ──────────────────────────────────────────────────────────

        private void BuildTriangleSprites()
        {
            _spriteRenderer.enabled = false;

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
                sr.sortingOrder = 0;
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
                    // Signed distance from the / diagonal (positive = inside the triangle).
                    float edge  = type == 0 ? (x - y) : (y - x);
                    float alpha = Mathf.Clamp01(edge + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            // PPU = size → 1 world unit = full sprite width.
            // Pivot (0,0) = bottom-left corner of the texture.
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0f), size);
        }

        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 wp = _camera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -_camera.transform.position.z));
            return wp;
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

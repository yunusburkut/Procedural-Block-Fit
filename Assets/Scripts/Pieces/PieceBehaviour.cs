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
        private MaterialPropertyBlock _mpb;

        private Vector2 _originalPosition;
        private Vector2 _dragOffset;
        private float   _cellSize;

        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private const int   DragSortBoost   = 1000;
        private const float DragScale       = 1.1f;

        private static int _globalSortCounter = 0;
        private int _myOrder = 0;

        public void Initialize(
            PieceData          data,
            float              cellSize,
            DifficultyConfig   config,
            SnapSystem         snapSystem,
            InputHandler       inputHandler,
            BoardController    board)
        {
            Data          = data;
            _cellSize     = cellSize;
            _config       = config;
            _snapSystem   = snapSystem;
            _inputHandler = inputHandler;
            _board        = board;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider       = GetComponent<PolygonCollider2D>();
            _mpb            = new MaterialPropertyBlock();

            _originalPosition = transform.position;

            BuildCellSprites();
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

        public void ApplyRotation(float degrees)
        {
            transform.Rotate(0f, 0f, degrees);

            int steps = Mathf.RoundToInt(degrees / 90f);
            var newOffsets = new Vector2Int[Data.cellOffsets.Length];
            for (int i = 0; i < Data.cellOffsets.Length; i++)
                newOffsets[i] = RotateOffset(Data.cellOffsets[i], steps);

            var updated = Data;
            updated.cellOffsets = newOffsets;
            Data = updated;

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

            _snapSystem.TrySnap(this, transform.position);

            SetDragging(false);
            _inputHandler.EndDrag();
        }

        private void SetDragging(bool dragging)
        {
            transform.localScale = dragging ? Vector3.one * DragScale : Vector3.one;

            int order = _myOrder + (dragging ? DragSortBoost : 0);
            foreach (Transform child in transform)
            {
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = order;
            }
        }

        private void BuildCellSprites()
        {

            _spriteRenderer.enabled = false;

            Sprite cellSprite = _spriteRenderer.sprite != null
                ? _spriteRenderer.sprite
                : CreateWhiteSquareSprite();

            int anchorFlat = Data.primaryAnchorFlat;
            int anchorCol  = anchorFlat % Data.gridSize;
            int anchorRow  = anchorFlat / Data.gridSize;

            foreach (int flat in Data.cells)
            {
                int col = flat % Data.gridSize;
                int row = flat / Data.gridSize;

                var cellGo = new GameObject($"Cell_{col}_{row}");
                cellGo.transform.SetParent(transform, false);
                cellGo.transform.localPosition = new Vector3(
                    (col - anchorCol) * _cellSize,
                    (row - anchorRow) * _cellSize,
                    0f);
                cellGo.transform.localScale = Vector3.one * _cellSize;

                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite       = cellSprite;
                sr.sortingOrder = 0;

                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorProp, (Color)Data.color);
                sr.SetPropertyBlock(mpb);
            }
        }

        private void BuildCollider()
        {
            int anchorFlat = Data.primaryAnchorFlat;
            int anchorCol  = anchorFlat % Data.gridSize;
            int anchorRow  = anchorFlat / Data.gridSize;

            _collider.pathCount = Data.cells.Length;
            float h = _cellSize * 0.5f;

            for (int i = 0; i < Data.cells.Length; i++)
            {
                int flat = Data.cells[i];
                int col  = flat % Data.gridSize;
                int row  = flat / Data.gridSize;

                float lx = (col - anchorCol) * _cellSize;
                float ly = (row - anchorRow) * _cellSize;

                _collider.SetPath(i, new[]
                {
                    new Vector2(lx - h, ly - h),
                    new Vector2(lx + h, ly - h),
                    new Vector2(lx + h, ly + h),
                    new Vector2(lx - h, ly + h),
                });
            }
        }

        private void BuildAnchorTransforms()
        {
            var anchorRoot = new GameObject("AnchorRoot");
            anchorRoot.transform.SetParent(transform, false);

            AnchorTransforms = new List<Transform>(Data.anchorCells.Length);

            int primaryFlat = Data.primaryAnchorFlat;
            int primaryCol  = primaryFlat % Data.gridSize;
            int primaryRow  = primaryFlat / Data.gridSize;

            foreach (int anchorFlat in Data.anchorCells)
            {
                int aCol = anchorFlat % Data.gridSize;
                int aRow = anchorFlat / Data.gridSize;

                var go = new GameObject($"Anchor_{aCol}_{aRow}");
                go.transform.SetParent(anchorRoot.transform, false);
                go.transform.localPosition = new Vector3(
                    (aCol - primaryCol) * _cellSize,
                    (aRow - primaryRow) * _cellSize,
                    0f);
                AnchorTransforms.Add(go.transform);
            }
        }

        private static Sprite CreateWhiteSquareSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private static Vector2 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
            return wp;
        }

        private static Vector2Int RotateOffset(Vector2Int offset, int steps90)
        {

            steps90 = ((steps90 % 4) + 4) % 4;
            Vector2Int v = offset;
            for (int i = 0; i < steps90; i++)
                v = new Vector2Int(-v.y, v.x);
            return v;
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
                Gizmos.DrawLine(p + new Vector3(-s, 0, 0), p + new Vector3( s, 0, 0));
                Gizmos.DrawLine(p + new Vector3(0, -s, 0), p + new Vector3(0,  s, 0));
            }
        }
#endif
    }
}

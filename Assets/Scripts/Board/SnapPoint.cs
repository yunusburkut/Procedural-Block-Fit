using UnityEngine;

namespace Blokfit.Board
{

    public class SnapPoint : MonoBehaviour
    {
        public int Col { get; private set; }
        public int Row { get; private set; }

        private bool _isOccupied;
        public bool IsOccupied
        {
            get => _isOccupied;
            set { _isOccupied = value; RefreshVisual(); }
        }

        public Vector2 WorldPosition => (Vector2)transform.position;

        private SpriteRenderer _sr;

        private static Sprite _sharedSprite;
        private static readonly Color FreeColor     = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color OccupiedColor = new Color(0f, 0f, 0f, 0f);

        public void Initialize(int col, int row, Vector2 worldPos, float cellSize)
        {
            Col = col;
            Row = row;
            _isOccupied = false;
            transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            gameObject.name = $"SnapPoint_{col}_{row}";

            BuildVisual(cellSize);
        }

        private void BuildVisual(float cellSize)
        {
            if (_sharedSprite == null)
                _sharedSprite = CreateWhiteSquareSprite();

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = _sharedSprite;
            _sr.sortingOrder = -1;
            _sr.color        = FreeColor;

            transform.localScale = Vector3.one * (cellSize * 0.85f);
        }

        private void RefreshVisual()
        {
            if (_sr != null)
                _sr.color = _isOccupied ? OccupiedColor : FreeColor;
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
    }
}

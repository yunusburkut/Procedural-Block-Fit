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
        private static readonly Color FreeColor     = new Color(1f, 1f, 1f, 0.22f);
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

            transform.localScale = Vector3.one * (cellSize * 0.18f);
        }

        private void RefreshVisual()
        {
            if (_sr != null)
                _sr.color = _isOccupied ? OccupiedColor : FreeColor;
        }

        private static Sprite CreateWhiteSquareSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };

            float center = (size - 1) * 0.5f;
            float radius = center;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

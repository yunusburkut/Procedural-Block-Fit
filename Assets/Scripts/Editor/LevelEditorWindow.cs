using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Blokfit.Core;

namespace Blokfit.LevelEditor
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Data
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    internal class EditorPiece
    {
        public string Name;
        public Color  Color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Editor Window
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tools ▶ Blokfit ▶ Level Editor
    ///
    /// Sol panel : grid boyutu + piece listesi (renk seçici, isim, sil).
    /// Sağ panel : N×N triangle grid; sol tık → seçili piece'e ata,
    ///             sağ tık → atamayı kaldır.
    /// Toolbar   : Temizle | ⬇ JSON Dışa Aktar
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ── Layout sabitleri ──────────────────────────────────────────────────
        private const float LeftW      = 240f;
        private const float GridPad    = 24f;
        private const float RowH       = 44f;

        // ── Renk paleti (yeni piece için otomatik) ────────────────────────────
        private static readonly Color[] Palette =
        {
            new(0.29f, 0.57f, 0.85f),   // mavi
            new(0.91f, 0.31f, 0.47f),   // pembe
            new(0.31f, 0.78f, 0.47f),   // yeşil
            new(0.96f, 0.65f, 0.14f),   // turuncu
            new(0.61f, 0.35f, 0.71f),   // mor
            new(0.10f, 0.74f, 0.61f),   // teal
            new(0.91f, 0.29f, 0.24f),   // kırmızı
            new(0.20f, 0.60f, 0.86f),   // açık mavi
            new(0.95f, 0.76f, 0.19f),   // sarı
            new(0.20f, 0.80f, 0.60f),   // nane
        };

        // ── Durum (domain reload'da korunur) ──────────────────────────────────
        [SerializeField] private int               _gridSize        = 4;
        [SerializeField] private int               _pendingGridSize = 4;
        [SerializeField] private List<EditorPiece> _pieces          = new();
        [SerializeField] private int               _selectedPiece   = -1;

        // Her triangleın sahibi: -1 = boş, >=0 = piece index
        // Düz indeks: (row * N + col) * 2 + type   (type 0=lower, 1=upper)
        [SerializeField] private int[]             _owner;

        private Vector2  _listScroll;

        // ── Menü ──────────────────────────────────────────────────────────────
        [MenuItem("Tools/Blokfit/Level Editor %&l")]
        public static void Open()
        {
            var win = GetWindow<LevelEditorWindow>("▦ Level Editor");
            win.minSize = new Vector2(760, 540);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void OnEnable()
        {
            int expected = _gridSize * _gridSize * 2;
            if (_owner == null || _owner.Length != expected)
                InitGrid(keepPieces: false);
        }

        private void InitGrid(bool keepPieces = false)
        {
            int total = _gridSize * _gridSize * 2;
            _owner = new int[total];
            for (int i = 0; i < total; i++) _owner[i] = -1;

            if (!keepPieces)
            {
                _pieces.Clear();
                _selectedPiece = -1;
            }
            else
            {
                // Tüm atamaları temizle ama piece listesini koru
                _selectedPiece = Mathf.Clamp(_selectedPiece, -1, _pieces.Count - 1);
            }

            Repaint();
        }

        // ── Ana OnGUI ─────────────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawTopBar();

            using var h = new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true));
            DrawLeftPanel();
            DrawGridPanel();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Top Bar
        // ─────────────────────────────────────────────────────────────────────
        private void DrawTopBar()
        {
            using var scope = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);

            GUILayout.Label("  ▦  Blokfit Level Editor", EditorStyles.boldLabel, GUILayout.Width(210));
            GUILayout.FlexibleSpace();

            // Özet istatistik
            int total      = _owner?.Length ?? 0;
            int unassigned = 0;
            if (_owner != null) foreach (var o in _owner) if (o < 0) unassigned++;
            GUILayout.Label($"Boş: {unassigned} / {total} tri", EditorStyles.miniLabel);
            GUILayout.Space(12);

            // Temizle
            var oldCol = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Temizle", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Temizle", "Tüm piece ve atamalar silinsin mi?", "Evet", "İptal"))
                    InitGrid(keepPieces: false);
            }
            GUI.color = oldCol;

            // Export
            GUI.color = new Color(0.55f, 1f, 0.65f);
            if (GUILayout.Button("⬇  JSON Dışa Aktar", EditorStyles.toolbarButton, GUILayout.Width(140)))
                ExportJson();
            GUI.color = oldCol;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sol Panel
        // ─────────────────────────────────────────────────────────────────────
        private void DrawLeftPanel()
        {
            using var v = new EditorGUILayout.VerticalScope(GUILayout.Width(LeftW), GUILayout.ExpandHeight(true));

            // ── Grid Ayarları ────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("⚙  Grid Ayarları", EditorStyles.boldLabel);

            _pendingGridSize = EditorGUILayout.IntSlider("Boyut (N)", _pendingGridSize, 2, 10);

            if (_pendingGridSize != _gridSize)
            {
                var oldCol = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.35f);
                if (GUILayout.Button($"Uygula  ({_gridSize} → {_pendingGridSize})", GUILayout.Height(22)))
                {
                    if (EditorUtility.DisplayDialog("Grid Boyutunu Değiştir",
                            $"Grid {_gridSize}×{_gridSize} → {_pendingGridSize}×{_pendingGridSize} yapılacak.\n" +
                            "Mevcut atamalar sıfırlanır, piece listesi korunur.",
                            "Uygula", "İptal"))
                    {
                        _gridSize = _pendingGridSize;
                        InitGrid(keepPieces: true);
                    }
                    else
                    {
                        _pendingGridSize = _gridSize;
                    }
                }
                GUI.color = oldCol;
            }

            DrawHR();

            // ── Piece Listesi ────────────────────────────────────────────────
            EditorGUILayout.LabelField("🎨  Piece'ler", EditorStyles.boldLabel);

            if (GUILayout.Button("＋  Yeni Piece", GUILayout.Height(26)))
            {
                _pieces.Add(new EditorPiece
                {
                    Name  = $"Piece {_pieces.Count}",
                    Color = Palette[_pieces.Count % Palette.Length],
                });
                _selectedPiece = _pieces.Count - 1;
                Repaint();
            }

            EditorGUILayout.Space(4);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _pieces.Count; i++)
                DrawPieceRow(i);

            EditorGUILayout.EndScrollView();

            DrawHR();

            // ── Yardım metni ─────────────────────────────────────────────────
            if (_selectedPiece >= 0 && _selectedPiece < _pieces.Count)
            {
                int cnt = CountTris(_selectedPiece);
                EditorGUILayout.HelpBox(
                    $"Aktif  →  {_pieces[_selectedPiece].Name}  ({cnt} tri)\n" +
                    "Sol tık: ata / sil    Sağ tık: atamayı kaldır",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Bir piece seçin veya oluşturun,\nsonra grid'deki triangle'lara tıklayın.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawPieceRow(int i)
        {
            bool isSel = (i == _selectedPiece);

            // Seçili piece için arka plan rengi
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isSel
                ? new Color(0.40f, 0.72f, 1.00f, 1f)
                : new Color(0.28f, 0.28f, 0.30f, 1f);

            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
                GUILayout.MinHeight(RowH));
            GUI.backgroundColor = oldBg;

            // ── Satır içi kontroller ─────────────────────────────────────────
            using (var row = new EditorGUILayout.HorizontalScope())
            {
                // Renk seçici
                _pieces[i].Color = EditorGUILayout.ColorField(
                    GUIContent.none, _pieces[i].Color,
                    showEyedropper: false, showAlpha: false, hdr: false,
                    GUILayout.Width(30), GUILayout.Height(20));

                // İsim alanı
                _pieces[i].Name = EditorGUILayout.TextField(_pieces[i].Name);

                // Seç / aktif göstergesi
                if (!isSel)
                {
                    if (GUILayout.Button("Seç", EditorStyles.miniButton, GUILayout.Width(38)))
                    {
                        _selectedPiece = i;
                        GUI.FocusControl(null);
                        Repaint();
                    }
                }
                else
                {
                    var oc = GUI.color;
                    GUI.color = new Color(0.35f, 1f, 0.45f);
                    GUILayout.Label("✔", EditorStyles.boldLabel, GUILayout.Width(18));
                    GUI.color = oc;
                }

                // Sil butonu
                var oc2 = GUI.color;
                GUI.color = new Color(1f, 0.40f, 0.40f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    GUI.color = oc2;
                    DeletePiece(i);
                    return;     // liste değişti, döngüyü durdur
                }
                GUI.color = oc2;
            }

            // Triangle sayısı
            int triCount = CountTris(i);
            EditorGUILayout.LabelField($"   {triCount} triangle", EditorStyles.miniLabel);

            // Satırın tamamına tıklamak → seç
            var lastRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                lastRect.Contains(Event.current.mousePosition))
            {
                _selectedPiece = i;
                Repaint();
                Event.current.Use();
            }

            EditorGUILayout.Space(2);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Grid Panel
        // ─────────────────────────────────────────────────────────────────────
        private void DrawGridPanel()
        {
            Rect panel = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            // Arkaplan
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(panel, new Color(0.11f, 0.11f, 0.13f));

            if (_owner == null) return;

            // ── Grid yerleşimi ───────────────────────────────────────────────
            float avW      = panel.width  - GridPad * 2f;
            float avH      = panel.height - GridPad * 2f;
            float cellSize = Mathf.Min(avW, avH) / _gridSize;
            float gridW    = cellSize * _gridSize;
            float gridH    = cellSize * _gridSize;
            float ox       = panel.x + (panel.width  - gridW) * 0.5f;
            float oy       = panel.y + (panel.height - gridH) * 0.5f;

            // ── Mouse olayları ───────────────────────────────────────────────
            Event e = Event.current;
            if (e.type == EventType.MouseDown && panel.Contains(e.mousePosition))
            {
                HandleGridClick(e.mousePosition, e.button, ox, oy, cellSize);
                e.Use();
                Repaint();
            }

            // ── Çizim (yalnızca Repaint) ─────────────────────────────────────
            if (e.type != EventType.Repaint) return;

            Handles.BeginGUI();

            for (int row = 0; row < _gridSize; row++)
            {
                for (int col = 0; col < _gridSize; col++)
                {
                    // Hücrenin ekran koordinatları
                    // row 0 = grid'in altı → ekranda en alt satır
                    float sx = ox + col                       * cellSize;
                    float sy = oy + (_gridSize - 1 - row)    * cellSize;   // ekran Y yukarıdan büyür
                    float ex = sx + cellSize;
                    float ey = sy + cellSize;

                    // type 0 (lower): BL-BR-TR  →  ekranda sol-alt, sağ-alt, sağ-üst
                    var lv = new Vector3[] { new(sx, ey), new(ex, ey), new(ex, sy) };
                    // type 1 (upper): BL-TR-TL  →  ekranda sol-alt, sağ-üst, sol-üst
                    var uv = new Vector3[] { new(sx, ey), new(ex, sy), new(sx, sy) };

                    int lFlat = (row * _gridSize + col) * 2;
                    int uFlat = lFlat + 1;

                    DrawTri(lv, lFlat);
                    DrawTri(uv, uFlat);
                }
            }

            // Grid label (sol üst köşe bilgisi)
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(panel.x + 8, panel.y + 4, 120, 18),
                $"Grid {_gridSize}×{_gridSize}  ({_gridSize * _gridSize * 2} tri)",
                EditorStyles.miniLabel);
            GUI.color = Color.white;

            Handles.EndGUI();
        }

        private void DrawTri(Vector3[] v, int flat)
        {
            int own = _owner[flat];

            // Dolgu rengi
            Color fill;
            if (own >= 0 && own < _pieces.Count)
            {
                fill = _pieces[own].Color;
                // Seçili piece'i hafif aydınlat
                if (own == _selectedPiece)
                    fill = Color.Lerp(fill, Color.white, 0.18f);
            }
            else
            {
                fill = new Color(0.20f, 0.20f, 0.23f);
            }

            // Dolgu
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(v);

            // Kenar çizgisi
            Handles.color = new Color(0f, 0f, 0f, 0.55f);
            Handles.DrawAAPolyLine(1.4f, v[0], v[1], v[2], v[0]);
        }

        private void HandleGridClick(Vector2 mp, int button, float ox, float oy, float cell)
        {
            for (int row = 0; row < _gridSize; row++)
            {
                for (int col = 0; col < _gridSize; col++)
                {
                    float sx = ox + col                    * cell;
                    float sy = oy + (_gridSize - 1 - row) * cell;
                    float ex = sx + cell;
                    float ey = sy + cell;

                    var lv = new Vector3[] { new(sx, ey), new(ex, ey), new(ex, sy) };
                    var uv = new Vector3[] { new(sx, ey), new(ex, sy), new(sx, sy) };

                    int lFlat = (row * _gridSize + col) * 2;
                    int uFlat = lFlat + 1;

                    if (PointInTri(mp, lv[0], lv[1], lv[2]))
                    { AssignTri(lFlat, button); return; }

                    if (PointInTri(mp, uv[0], uv[1], uv[2]))
                    { AssignTri(uFlat, button); return; }
                }
            }
        }

        private void AssignTri(int flat, int mouseButton)
        {
            if (mouseButton == 1)
            {
                // Sağ tık → her zaman atamayı kaldır
                _owner[flat] = -1;
                return;
            }

            // Sol tık
            if (_selectedPiece < 0 || _selectedPiece >= _pieces.Count) return;

            // Zaten bu piece'e atanmışsa → kaldır (toggle)
            _owner[flat] = (_owner[flat] == _selectedPiece) ? -1 : _selectedPiece;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Export JSON
        // ─────────────────────────────────────────────────────────────────────
        private void ExportJson()
        {
            if (_pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Hata", "Önce en az bir piece ekleyin.", "Tamam");
                return;
            }

            // Boş triangle uyarısı
            int unassigned = 0;
            if (_owner != null) foreach (var o in _owner) if (o < 0) unassigned++;
            if (unassigned > 0)
            {
                bool proceed = EditorUtility.DisplayDialog("Uyarı",
                    $"{unassigned} triangle herhangi bir piece'e atanmamış.\n" +
                    "Yine de devam edilsin mi?",
                    "Devam", "İptal");
                if (!proceed) return;
            }

            // PieceJson listesi oluştur
            var pieceJsons = new List<PieceJson>();
            for (int pi = 0; pi < _pieces.Count; pi++)
            {
                var cells = new List<int>();
                for (int ti = 0; ti < _owner!.Length; ti++)
                    if (_owner[ti] == pi) cells.Add(ti);

                if (cells.Count == 0) continue;   // boş piece → atla

                // anchor = en küçük flat indeks
                int anchor = cells[0];
                foreach (int c in cells) if (c < anchor) anchor = c;

                string hex = ColorUtility.ToHtmlStringRGB(_pieces[pi].Color);

                pieceJsons.Add(new PieceJson
                {
                    cells   = cells.ToArray(),
                    color   = "#" + hex,
                    anchors = new[] { anchor },
                    spawnX  = 0f,
                    spawnY  = 0f,
                });
            }

            if (pieceJsons.Count == 0)
            {
                EditorUtility.DisplayDialog("Hata", "Hiçbir piece'e triangle atanmamış.", "Tamam");
                return;
            }

            var levelData = new LevelData
            {
                grid   = new GridData { size = _gridSize },
                pieces = pieceJsons.ToArray(),
            };

            string json = LevelSerializer.ToJson(levelData);

            // Kayıt yerini seç
            string defaultDir = Path.Combine(Application.dataPath, "Resources", "Levels");
            Directory.CreateDirectory(defaultDir);

            string path = EditorUtility.SaveFilePanel(
                "Level JSON Kaydet", defaultDir, "level_handmade", "json");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, json);

            // Assets içindeyse refresh
            if (path.StartsWith(Application.dataPath))
            {
                string rel = "Assets" + path[Application.dataPath.Length..];
                AssetDatabase.ImportAsset(rel);
            }
            AssetDatabase.Refresh();

            Debug.Log($"[LevelEditor] Kaydedildi → {path}");
            EditorUtility.DisplayDialog("✔  Kaydedildi", $"Dosya:\n{path}", "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Yardımcılar
        // ─────────────────────────────────────────────────────────────────────

        private void DeletePiece(int idx)
        {
            if (_owner != null)
                for (int i = 0; i < _owner.Length; i++)
                {
                    if (_owner[i] == idx)      _owner[i] = -1;
                    else if (_owner[i] > idx)  _owner[i]--;
                }

            _pieces.RemoveAt(idx);
            _selectedPiece = Mathf.Clamp(_selectedPiece, -1, _pieces.Count - 1);
            Repaint();
        }

        private int CountTris(int pieceIdx)
        {
            if (_owner == null) return 0;
            int cnt = 0;
            foreach (var o in _owner) if (o == pieceIdx) cnt++;
            return cnt;
        }

        private static void DrawHR()
        {
            EditorGUILayout.Space(5);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// İşaret metoduyla nokta-üçgen içi testi.
        /// </summary>
        private static bool PointInTri(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            static float Cross(Vector2 pt, Vector3 u, Vector3 v)
                => (u.x - pt.x) * (v.y - pt.y) - (u.y - pt.y) * (v.x - pt.x);

            float d1 = Cross(p, a, b);
            float d2 = Cross(p, b, c);
            float d3 = Cross(p, c, a);

            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }
    }
}

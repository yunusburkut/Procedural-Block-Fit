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
    /// Left panel : grid size + piece list (colour picker, name, delete).
    /// Right panel : N×N triangle grid; left-click → assign to selected piece,
    ///               right-click → unassign.
    /// Toolbar    : Clear | ⬇ Export JSON
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const float LeftW      = 240f;
        private const float GridPad    = 24f;
        private const float RowH       = 44f;

        // ── Colour palette (auto-assigned to new pieces) ──────────────────────
        private static readonly Color[] Palette =
        {
            new(0.29f, 0.57f, 0.85f),   // blue
            new(0.91f, 0.31f, 0.47f),   // pink
            new(0.31f, 0.78f, 0.47f),   // green
            new(0.96f, 0.65f, 0.14f),   // orange
            new(0.61f, 0.35f, 0.71f),   // purple
            new(0.10f, 0.74f, 0.61f),   // teal
            new(0.91f, 0.29f, 0.24f),   // red
            new(0.20f, 0.60f, 0.86f),   // light blue
            new(0.95f, 0.76f, 0.19f),   // yellow
            new(0.20f, 0.80f, 0.60f),   // mint
        };

        // ── State (survives domain reload) ───────────────────────────────────
        [SerializeField] private int               _gridSize        = 4;
        [SerializeField] private int               _pendingGridSize = 4;
        [SerializeField] private List<EditorPiece> _pieces          = new();
        [SerializeField] private int               _selectedPiece   = -1;

        // Per-triangle owner: -1 = unassigned, >=0 = piece index
        // Flat index: (row * N + col) * 2 + type   (type 0=lower, 1=upper)
        [SerializeField] private int[]             _owner;

        private Vector2  _listScroll;

        // ── Menu ──────────────────────────────────────────────────────────────
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
                // Clear all assignments but keep the piece list
                _selectedPiece = Mathf.Clamp(_selectedPiece, -1, _pieces.Count - 1);
            }

            Repaint();
        }

        // ── Main OnGUI ────────────────────────────────────────────────────────
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

            // Summary stats
            int total      = _owner?.Length ?? 0;
            int unassigned = 0;
            if (_owner != null) foreach (var o in _owner) if (o < 0) unassigned++;
            GUILayout.Label($"Unassigned: {unassigned} / {total} tri", EditorStyles.miniLabel);
            GUILayout.Space(12);

            // Clear
            var oldCol = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Clear", "Delete all pieces and assignments?", "Yes", "Cancel"))
                    InitGrid(keepPieces: false);
            }
            GUI.color = oldCol;

            // Export
            GUI.color = new Color(0.55f, 1f, 0.65f);
            if (GUILayout.Button("⬇  Export JSON", EditorStyles.toolbarButton, GUILayout.Width(140)))
                ExportJson();
            GUI.color = oldCol;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Left Panel
        // ─────────────────────────────────────────────────────────────────────
        private void DrawLeftPanel()
        {
            using var v = new EditorGUILayout.VerticalScope(GUILayout.Width(LeftW), GUILayout.ExpandHeight(true));

            // ── Grid Settings ────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("⚙  Grid Settings", EditorStyles.boldLabel);

            _pendingGridSize = EditorGUILayout.IntSlider("Size (N)", _pendingGridSize, 2, 10);

            if (_pendingGridSize != _gridSize)
            {
                var oldCol = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.35f);
                if (GUILayout.Button($"Apply  ({_gridSize} → {_pendingGridSize})", GUILayout.Height(22)))
                {
                    if (EditorUtility.DisplayDialog("Change Grid Size",
                            $"Grid will be resized: {_gridSize}×{_gridSize} → {_pendingGridSize}×{_pendingGridSize}.\n" +
                            "All assignments will be cleared; the piece list will be kept.",
                            "Apply", "Cancel"))
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

            // ── Piece List ───────────────────────────────────────────────────
            EditorGUILayout.LabelField("🎨  Pieces", EditorStyles.boldLabel);

            if (GUILayout.Button("＋  New Piece", GUILayout.Height(26)))
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

            // ── Help text ────────────────────────────────────────────────────
            if (_selectedPiece >= 0 && _selectedPiece < _pieces.Count)
            {
                int cnt = CountTris(_selectedPiece);
                EditorGUILayout.HelpBox(
                    $"Active  →  {_pieces[_selectedPiece].Name}  ({cnt} tri)\n" +
                    "Left-click: assign / remove    Right-click: unassign",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Select or create a piece,\nthen click triangles in the grid.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawPieceRow(int i)
        {
            bool isSel = (i == _selectedPiece);

            // Background colour for the selected piece row
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isSel
                ? new Color(0.40f, 0.72f, 1.00f, 1f)
                : new Color(0.28f, 0.28f, 0.30f, 1f);

            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
                GUILayout.MinHeight(RowH));
            GUI.backgroundColor = oldBg;

            // ── Row controls ─────────────────────────────────────────────────
            using (var row = new EditorGUILayout.HorizontalScope())
            {
                // Colour picker
                _pieces[i].Color = EditorGUILayout.ColorField(
                    GUIContent.none, _pieces[i].Color,
                    showEyedropper: false, showAlpha: false, hdr: false,
                    GUILayout.Width(30), GUILayout.Height(20));

                // Name field
                _pieces[i].Name = EditorGUILayout.TextField(_pieces[i].Name);

                // Select / active indicator
                if (!isSel)
                {
                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(46)))
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

                // Delete button
                var oc2 = GUI.color;
                GUI.color = new Color(1f, 0.40f, 0.40f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    GUI.color = oc2;
                    DeletePiece(i);
                    return;     // list changed, stop the loop
                }
                GUI.color = oc2;
            }

            // Triangle count
            int triCount = CountTris(i);
            EditorGUILayout.LabelField($"   {triCount} triangle", EditorStyles.miniLabel);

            // Clicking anywhere on the row → select
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

            // Background
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(panel, new Color(0.11f, 0.11f, 0.13f));

            if (_owner == null) return;

            // ── Grid layout ──────────────────────────────────────────────────
            float avW      = panel.width  - GridPad * 2f;
            float avH      = panel.height - GridPad * 2f;
            float cellSize = Mathf.Min(avW, avH) / _gridSize;
            float gridW    = cellSize * _gridSize;
            float gridH    = cellSize * _gridSize;
            float ox       = panel.x + (panel.width  - gridW) * 0.5f;
            float oy       = panel.y + (panel.height - gridH) * 0.5f;

            // ── Mouse events ─────────────────────────────────────────────────
            Event e = Event.current;
            if (e.type == EventType.MouseDown && panel.Contains(e.mousePosition))
            {
                HandleGridClick(e.mousePosition, e.button, ox, oy, cellSize);
                e.Use();
                Repaint();
            }

            // ── Drawing (Repaint only) ───────────────────────────────────────
            if (e.type != EventType.Repaint) return;

            Handles.BeginGUI();

            for (int row = 0; row < _gridSize; row++)
            {
                for (int col = 0; col < _gridSize; col++)
                {
                    // Screen coordinates for this cell
                    // row 0 = bottom of the grid → lowest row on screen
                    float sx = ox + col                       * cellSize;
                    float sy = oy + (_gridSize - 1 - row)    * cellSize;   // screen Y grows downward
                    float ex = sx + cellSize;
                    float ey = sy + cellSize;

                    // type 0 (lower): BL-BR-TR  →  screen: bottom-left, bottom-right, top-right
                    var lv = new Vector3[] { new(sx, ey), new(ex, ey), new(ex, sy) };
                    // type 1 (upper): BL-TR-TL  →  screen: bottom-left, top-right, top-left
                    var uv = new Vector3[] { new(sx, ey), new(ex, sy), new(sx, sy) };

                    int lFlat = (row * _gridSize + col) * 2;
                    int uFlat = lFlat + 1;

                    DrawTri(lv, lFlat);
                    DrawTri(uv, uFlat);
                }
            }

            // Grid label (top-left corner info)
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

            // Fill colour
            Color fill;
            if (own >= 0 && own < _pieces.Count)
            {
                fill = _pieces[own].Color;
                // Brighten the active piece slightly
                if (own == _selectedPiece)
                    fill = Color.Lerp(fill, Color.white, 0.18f);
            }
            else
            {
                fill = new Color(0.20f, 0.20f, 0.23f);
            }

            // Fill
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(v);

            // Outline
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
                // Right-click → always unassign
                _owner[flat] = -1;
                return;
            }

            // Left-click
            if (_selectedPiece < 0 || _selectedPiece >= _pieces.Count) return;

            // Already assigned to this piece → remove (toggle)
            _owner[flat] = (_owner[flat] == _selectedPiece) ? -1 : _selectedPiece;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Export JSON
        // ─────────────────────────────────────────────────────────────────────
        private void ExportJson()
        {
            if (_pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Add at least one piece first.", "OK");
                return;
            }

            // Warn about unassigned triangles
            int unassigned = 0;
            if (_owner != null) foreach (var o in _owner) if (o < 0) unassigned++;
            if (unassigned > 0)
            {
                bool proceed = EditorUtility.DisplayDialog("Warning",
                    $"{unassigned} triangle(s) are not assigned to any piece.\n" +
                    "Continue anyway?",
                    "Continue", "Cancel");
                if (!proceed) return;
            }

            // Build PieceJson list
            var pieceJsons = new List<PieceJson>();
            for (int pi = 0; pi < _pieces.Count; pi++)
            {
                var cells = new List<int>();
                for (int ti = 0; ti < _owner!.Length; ti++)
                    if (_owner[ti] == pi) cells.Add(ti);

                if (cells.Count == 0) continue;   // empty piece → skip

                // anchor = smallest flat index
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
                EditorUtility.DisplayDialog("Error", "No triangles have been assigned to any piece.", "OK");
                return;
            }

            var levelData = new LevelData
            {
                grid   = new GridData { size = _gridSize },
                pieces = pieceJsons.ToArray(),
            };

            string json = LevelSerializer.ToJson(levelData);

            // Choose save location
            string defaultDir = Path.Combine(Application.dataPath, "Resources", "Levels");
            Directory.CreateDirectory(defaultDir);

            string path = EditorUtility.SaveFilePanel(
                "Save Level JSON", defaultDir, "level_handmade", "json");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, json);

            // Refresh if inside Assets
            if (path.StartsWith(Application.dataPath))
            {
                string rel = "Assets" + path[Application.dataPath.Length..];
                AssetDatabase.ImportAsset(rel);
            }
            AssetDatabase.Refresh();

            Debug.Log($"[LevelEditor] Saved → {path}");
            EditorUtility.DisplayDialog("✔  Saved", $"File:\n{path}", "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
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
        /// Point-in-triangle test using the sign method.
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

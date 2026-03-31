using UnityEngine;
using Blokfit.Core;

namespace Blokfit.Pieces
{
    /// <summary>
    /// Converts raw JSON data into runtime <see cref="PieceData"/> objects.
    /// Translates absolute triangle flat-indices into piece-local <see cref="TriOffset"/> values
    /// relative to the primary anchor.
    /// </summary>
    public static class PieceFactory
    {
        /// <summary>
        /// Builds a <see cref="PieceData"/> from a <see cref="PieceJson"/> entry.
        /// All cell indices are converted to (dcol, drow, type) offsets from the anchor cell.
        /// </summary>
        public static PieceData FromJson(PieceJson json, int gridSize)
        {
            int primaryAnchorFlat = json.anchors != null && json.anchors.Length > 0
                ? json.anchors[0]
                : json.cells[0];

            int anchorCellFlat = primaryAnchorFlat / 2;
            int anchorCol      = anchorCellFlat % gridSize;
            int anchorRow      = anchorCellFlat / gridSize;

            var offsets = new TriOffset[json.cells.Length];
            for (int i = 0; i < json.cells.Length; i++)
            {
                int triFlat  = json.cells[i];
                int cellFlat = triFlat / 2;
                int type     = triFlat % 2;
                int col      = cellFlat % gridSize;
                int row      = cellFlat / gridSize;
                offsets[i] = new TriOffset(col - anchorCol, row - anchorRow, type);
            }

            return new PieceData(
                triangleOffsets: offsets,
                color:           ParseHexColor(json.color)
            );
        }

        private static readonly Color32 FallbackColor = new Color32(200, 200, 200, 255);

        /// <summary>Parses an HTML hex string (e.g. "#4A90D9") into a Color32. Falls back to grey on failure.</summary>
        private static Color32 ParseHexColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return FallbackColor;
        }
    }
}

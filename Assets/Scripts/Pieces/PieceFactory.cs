using UnityEngine;
using Blokfit.Core;

namespace Blokfit.Pieces
{
    public static class PieceFactory
    {
        public static PieceData FromJson(PieceJson json, int gridSize)
        {
            int primaryAnchorFlat = json.anchors != null && json.anchors.Length > 0
                ? json.anchors[0]
                : json.cells[0];

            // Anchor vertex = bottom-left corner of the anchor triangle's cell.
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
                cells:             json.cells,
                triangleOffsets:   offsets,
                anchorCells:       json.anchors ?? new[] { json.cells[0] },
                color:             ParseHexColor(json.color),
                gridSize:          gridSize,
                primaryAnchorFlat: primaryAnchorFlat
            );
        }

        private static Color32 ParseHexColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return new Color32(200, 200, 200, 255);
        }
    }
}

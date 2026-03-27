using System;
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
                offsets[i] = new TriOffset
                {
                    dcol = col - anchorCol,
                    drow = row - anchorRow,
                    type = type,
                };
            }

            return new PieceData
            {
                cells             = json.cells,
                triangleOffsets   = offsets,
                anchorCells       = json.anchors ?? new[] { json.cells[0] },
                color             = ParseHexColor(json.color),
                gridSize          = gridSize,
                primaryAnchorFlat = primaryAnchorFlat,
            };
        }

        private static Color32 ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return new Color32(200, 200, 200, 255);

            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color32(r, g, b, 255);
            }
            if (hex.Length == 8)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = Convert.ToByte(hex.Substring(6, 2), 16);
                return new Color32(r, g, b, a);
            }

            return new Color32(200, 200, 200, 255);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.FarField
{
    /// <summary>
    /// Picks what the far layer draws over the padded view rectangle, in field-relative plane space.
    /// Cells group into square blocks; a block whose nearest point lies past the unload radius holds no
    /// loaded cell (the streamer drops every chunk whose centre leaves that radius), so it is drawn
    /// whole without asking. Only cells of the remaining boundary blocks are tested one by one, and
    /// only those whose centre is within the unload radius can be real. The quick reject relies on the
    /// streamer loading every cell whose centre lies within the load radius of the anchor.
    /// </summary>
    public static class FarFieldCells
    {
        public const int BlockCells = 16;

        private static readonly float HalfDiagonal = Mathf.Sqrt(2f) * 0.5f;

        public static Rect Expand(Rect view, float pad) =>
            Rect.MinMaxRect(view.xMin - pad, view.yMin - pad, view.xMax + pad, view.yMax + pad);

        public static bool AllLoaded(Rect expanded, Vector2 anchor, float loadRadius, float cellSize) =>
            FarthestCorner(expanded, anchor) + cellSize * HalfDiagonal <= loadRadius;

        public static Vector2Int BlockOf(Vector2Int cell) =>
            new(FloorDiv(cell.x, BlockCells), FloorDiv(cell.y, BlockCells));

        public static Vector2Int FirstCell(Vector2Int block) => block * BlockCells;

        public static bool IsWholeFar(Vector2Int block, Vector2 anchor, float unloadRadius, float cellSize)
        {
            var size = BlockCells * cellSize;
            var min = new Vector2(block.x * size, block.y * size);
            var nearest = Vector2.Max(min, Vector2.Min(anchor, min + new Vector2(size, size)));
            return (nearest - anchor).sqrMagnitude > unloadRadius * unloadRadius;
        }

        public static bool CanBeLoaded(Vector2Int cell, Vector2 anchor, float unloadRadius, float cellSize)
        {
            var centre = new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
            return (centre - anchor).sqrMagnitude <= unloadRadius * unloadRadius;
        }

        public static RectInt CellRange(Rect expanded, float cellSize)
        {
            var minX = Mathf.FloorToInt(expanded.xMin / cellSize);
            var minY = Mathf.FloorToInt(expanded.yMin / cellSize);
            var maxX = Mathf.FloorToInt(expanded.xMax / cellSize);
            var maxY = Mathf.FloorToInt(expanded.yMax / cellSize);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Splits the blocks overlapping <paramref name="expanded"/> into whole-far and boundary blocks, and
        /// lists the boundary blocks' far cells that overlap it. Returns whether anything is far.
        /// </summary>
        public static bool Select(Rect expanded, Vector2 anchor, float loadRadius, float unloadRadius, float cellSize,
            Func<Vector2Int, bool> isReal, List<Vector2Int> wholeFarBlocks, List<Vector2Int> boundaryBlocks,
            List<Vector2Int> boundaryFarCells)
        {
            wholeFarBlocks.Clear();
            boundaryBlocks.Clear();
            boundaryFarCells.Clear();
            if (AllLoaded(expanded, anchor, loadRadius, cellSize)) return false;

            var cells = CellRange(expanded, cellSize);
            var blockMin = BlockOf(cells.min);
            var blockMax = BlockOf(cells.max - Vector2Int.one);
            for (var by = blockMin.y; by <= blockMax.y; by++)
            for (var bx = blockMin.x; bx <= blockMax.x; bx++)
            {
                var block = new Vector2Int(bx, by);
                if (IsWholeFar(block, anchor, unloadRadius, cellSize))
                {
                    wholeFarBlocks.Add(block);
                    continue;
                }
                boundaryBlocks.Add(block);

                var first = FirstCell(block);
                var minX = Mathf.Max(first.x, cells.xMin);
                var minY = Mathf.Max(first.y, cells.yMin);
                var maxX = Mathf.Min(first.x + BlockCells, cells.xMax);
                var maxY = Mathf.Min(first.y + BlockCells, cells.yMax);
                for (var cy = minY; cy < maxY; cy++)
                for (var cx = minX; cx < maxX; cx++)
                {
                    var cell = new Vector2Int(cx, cy);
                    if (!CanBeLoaded(cell, anchor, unloadRadius, cellSize) || !isReal(cell)) boundaryFarCells.Add(cell);
                }
            }
            return wholeFarBlocks.Count > 0 || boundaryFarCells.Count > 0;
        }

        public static bool Overlaps(Rect rect, Vector2Int cell, float cellSize) =>
            (cell.x + 1) * cellSize >= rect.xMin && cell.x * cellSize <= rect.xMax
            && (cell.y + 1) * cellSize >= rect.yMin && cell.y * cellSize <= rect.yMax;

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

        private static float FarthestCorner(Rect rect, Vector2 anchor)
        {
            var dx = Mathf.Max(Mathf.Abs(rect.xMin - anchor.x), Mathf.Abs(rect.xMax - anchor.x));
            var dy = Mathf.Max(Mathf.Abs(rect.yMin - anchor.y), Mathf.Abs(rect.yMax - anchor.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}

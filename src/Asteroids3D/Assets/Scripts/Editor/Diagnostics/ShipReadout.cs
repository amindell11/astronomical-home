using System;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Which stacked status row a diagnostic writes. Declaration order is bottom-to-top stack order and each row reserves <see cref="ShipReadout.ReservedLines"/> lines, so a row sits in the same place whether or not the others drew.</summary>
    public enum ShipReadoutRow
    {
        Speed,
        Shield,
        Health,
        Heat,
        Missiles,
        LockOn,
        Policy,
    }

    /// <summary>Places ship status text so independently-drawn diagnostics never collide. Placement and styling only — geometry stays with the drawer that owns it.</summary>
    public static class ShipReadout
    {
        private const float TextSize = 3f;
        private const float LinePitch = 1.1f;
        private const float LineHeight = TextSize * LinePitch;
        private const float BaseOffset = 3f;

        /// <summary>Lines each row is allowed, indexed by <see cref="ShipReadoutRow"/>. A row's reservation is what holds every row above it in place.</summary>
        private static readonly int[] ReservedLines = { 1, 1, 1, 1, 2, 3, 4 };

        public static void Draw(Vector2 subject, ShipReadoutRow row, string text, Color color)
        {
            var reserved = ReservedLines[(int)row];
            var lines = LineCount(text);
            if (lines > reserved)
                throw new ArgumentException(
                    $"{row} readout has {lines} lines but reserves {reserved}; raise its {nameof(ReservedLines)} entry or the rows above it will overlap.",
                    nameof(text));

            var y = BaseOffset + LinesBelow(row) * LineHeight + reserved * LineHeight * 0.5f;
            Handles.Label(GamePlane.PlanePointToWorld(subject + new Vector2(0f, y)), text,
                new GUIStyle { normal = { textColor = color }, fontSize = Mathf.RoundToInt(TextSize * 3f) });
        }

        private static int LinesBelow(ShipReadoutRow row)
        {
            var lines = 0;
            for (var i = 0; i < (int)row; i++) lines += ReservedLines[i];
            return lines;
        }

        private static int LineCount(string text)
        {
            var lines = 1;
            for (var i = 0; i < text.Length; i++)
                if (text[i] == '\n')
                    lines++;
            return lines;
        }
    }
}

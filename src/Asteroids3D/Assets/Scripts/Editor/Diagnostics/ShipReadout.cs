using System;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Which stacked status row a diagnostic writes. Declaration order is bottom-to-top stack order and each row reserves <see cref="ShipReadout.ReservedLines"/> lines, so a row sits in the same place whether or not the others drew.</summary>
    public enum ShipReadoutRow
    {
        Speed,
        Controls,
        Shield,
        Health,
        Heat,
        Missiles,
        LockOn,
        Policy,
    }

    /// <summary>Places ship status rows so independently-drawn diagnostics never collide. The stack is laid out in screen space — a pixel line pitch above the subject's projection — so layout tracks the fixed-size fonts at every zoom. Placement and styling only — geometry stays with the drawer that owns it.</summary>
    public static class ShipReadout
    {
        private const int FontSize = 11;
        private const float LinePitch = 14f;
        private const float HalfWidth = 110f;
        // Base: the world offset clears the in-plane bars zoomed in; the pixel floor clears the ship zoomed out.
        private const float BaseWorldOffset = 3f;
        private const float MinBasePixels = 24f;

        /// <summary>Lines each row is allowed, indexed by <see cref="ShipReadoutRow"/>. A row's reservation is what holds every row above it in place.</summary>
        private static readonly int[] ReservedLines = { 1, 3, 1, 1, 1, 2, 3, 4 };

        public static void Draw(Vector2 subject, ShipReadoutRow row, string text, Color color)
        {
            var reserved = ReservedLines[(int)row];
            var lines = LineCount(text);
            if (lines > reserved)
                throw new ArgumentException(
                    $"{row} readout has {lines} lines but reserves {reserved}; raise its {nameof(ReservedLines)} entry or the rows above it will overlap.",
                    nameof(text));

            if (!TryGetRowRect(subject, row, out var rect)) return;
            Handles.BeginGUI();
            GUI.Label(rect, text, new GUIStyle
            {
                normal = { textColor = color },
                fontSize = FontSize,
                alignment = TextAnchor.LowerCenter,
            });
            Handles.EndGUI();
        }

        /// <summary>The GUI-space band a row reserves, for drawers stacking non-text content. False with no rendering camera or a subject behind it.</summary>
        public static bool TryGetRowRect(Vector2 subject, ShipReadoutRow row, out Rect rect)
        {
            rect = default;
            if (!Camera.current) return false;

            var ship = HandleUtility.WorldToGUIPointWithDepth(GamePlane.PlanePointToWorld(subject));
            if (ship.z < 0f) return false;
            var basePoint = HandleUtility.WorldToGUIPointWithDepth(
                GamePlane.PlanePointToWorld(subject + new Vector2(0f, BaseWorldOffset)));

            var baseY = Mathf.Min(basePoint.y, ship.y - MinBasePixels);
            var bottom = baseY - LinesBelow(row) * LinePitch;
            var height = ReservedLines[(int)row] * LinePitch;
            rect = new Rect(ship.x - HalfWidth, bottom - height, HalfWidth * 2f, height);
            return true;
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

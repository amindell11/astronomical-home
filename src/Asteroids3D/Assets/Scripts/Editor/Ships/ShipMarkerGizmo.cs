using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Ships
{
    /// <summary>The default-on ship marker: a solid filled chevron at each in-scope ship, pointing along
    /// its nose so facing reads at a glance. Zoom-stable via <see cref="HandleUtility.GetHandleSize"/> so it
    /// neither balloons nor vanishes. Facing comes from the transform, so it draws in edit mode too.</summary>
    [InitializeOnLoad]
    internal static class ShipMarkerGizmo
    {
        static ShipMarkerGizmo() =>
            GizmoView.Register(typeof(Ship), "marker", "Facing Marker",
                "solid orange chevron pointing along ship nose", "Ship", defaultOn: true);

        private const float SizeFactor = 0.7f;
        private const float HalfWidthFactor = 0.6f;
        private const float TailFactor = 0.5f;

        private static readonly Color MarkerColor = new(1f, 0.6f, 0.15f, 0.9f);

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Ship))]
        private static void Draw(Ship ship, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(Ship), "marker") || !GizmoView.InScope(ship)) return;

            var pos = ship.transform.position;
            var forward = ship.transform.up;
            if (forward.sqrMagnitude < 1e-6f) return;
            forward.Normalize();
            var right = Vector3.Cross(GamePlane.Normal, forward).normalized;

            var size = HandleUtility.GetHandleSize(pos) * SizeFactor;
            var tip = pos + forward * size;
            var backLeft = pos - forward * (size * TailFactor) - right * (size * HalfWidthFactor);
            var backRight = pos - forward * (size * TailFactor) + right * (size * HalfWidthFactor);

            Handles.color = MarkerColor;
            Handles.DrawAAConvexPolygon(tip, backRight, backLeft);
        }
    }
}

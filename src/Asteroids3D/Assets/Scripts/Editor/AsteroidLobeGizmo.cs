using Asteroids;
using Asteroids.Spawning;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Editor-only Scene-view visualization of the baked lobe decomposition. It recomputes
    /// the lobes from the same deterministic baker (reads nothing at runtime) and draws each
    /// as a cyan wire sphere; visibility is gated by the Gizmo View window.
    /// </summary>
    [InitializeOnLoad]
    public static class AsteroidLobeGizmo
    {
        static AsteroidLobeGizmo() =>
            GizmoView.Register(typeof(AsteroidController), "lobes", "Lobe Decomposition",
                "cyan wire-sphere lobe decomposition", "Environment");

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AsteroidController))]
        private static void DrawLobes(AsteroidController ctrl, GizmoType t)
        {
            if (!GizmoView.IsOn(typeof(AsteroidController), "lobes") || !GizmoView.InScope(ctrl)) return;
            var mf = ctrl.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return;

            var lobes = AsteroidLobeBaker.Bake(mesh, out _);
            if (lobes == null) return;

            Gizmos.color = Color.cyan;
            float scale = ctrl.transform.lossyScale.x;
            foreach (var lobe in lobes)
                Gizmos.DrawWireSphere(ctrl.transform.TransformPoint(lobe.center), lobe.radius * scale);
        }
    }
}

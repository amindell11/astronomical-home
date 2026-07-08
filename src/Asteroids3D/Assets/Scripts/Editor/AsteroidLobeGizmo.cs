using Asteroids;
using Asteroids.Spawning;
using UnityEditor;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Editor-only Scene-view visualization of the baked lobe decomposition. For a
    /// selected/active asteroid it recomputes the lobes from the same deterministic
    /// baker (reads nothing at runtime) and draws each as a cyan wire sphere.
    /// </summary>
    public static class AsteroidLobeGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active, typeof(AsteroidController))]
        private static void DrawLobes(AsteroidController ctrl, GizmoType t)
        {
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

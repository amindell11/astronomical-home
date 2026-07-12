using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    internal static class MissilesGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missiles))]
        private static void DrawAmmoLabel(Missiles missiles, GizmoType gizmoType)
        {
            if (!missiles.firePoint || !missiles.Rounds) return;
            var ammoText = $"Ammo: {missiles.Rounds.AmmoCount}/{missiles.Rounds.MaxAmmo}";
            Handles.Label(missiles.firePoint.position + Vector3.up * 2f, $"Missiles\n{ammoText}");
        }
    }
}

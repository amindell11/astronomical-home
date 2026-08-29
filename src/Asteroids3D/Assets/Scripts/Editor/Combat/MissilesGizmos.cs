using System.Runtime.CompilerServices;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Missile-launcher ammo, stacked with the rest of the ship's status rows.</summary>
    [InitializeOnLoad]
    internal static class MissilesGizmos
    {
        static MissilesGizmos() =>
            GizmoView.Register(typeof(Missiles), "ammo", "Missile Ammo",
                "missile ammo count readout row", "Combat");

        private static readonly ConditionalWeakTable<Missiles, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missiles))]
        private static void DrawAmmoReadout(Missiles missiles, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(Missiles), "ammo") || !GizmoView.InScope(missiles)) return;
            if (!Application.isPlaying || !missiles.Rounds) return;
            var ship = ParentShips.GetValue(missiles, m => m.GetComponentInParent<Ship>());
            if (!ship) return;

            ShipReadout.Draw(ship.Kinematics.pos, ShipReadoutRow.Missiles,
                $"Missiles\nAmmo: {missiles.Rounds.AmmoCount}/{missiles.Rounds.MaxAmmo}", Color.white);
        }
    }
}

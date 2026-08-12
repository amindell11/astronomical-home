using System.Runtime.CompilerServices;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Missile-launcher ammo, stacked with the rest of the ship's status rows.</summary>
    internal static class MissilesGizmos
    {
        private static readonly ConditionalWeakTable<Missiles, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected, typeof(Missiles))]
        private static void DrawAmmoReadout(Missiles missiles, GizmoType gizmoType)
        {
            if (!Application.isPlaying || !missiles.Rounds) return;
            var ship = ParentShips.GetValue(missiles, m => m.GetComponentInParent<Ship>());
            if (!ship) return;

            ShipReadout.Draw(ship.Kinematics.pos, ShipReadoutRow.Missiles,
                $"Missiles\nAmmo: {missiles.Rounds.AmmoCount}/{missiles.Rounds.MaxAmmo}", Color.white);
        }
    }
}

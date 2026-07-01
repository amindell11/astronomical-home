using System.Collections.Generic;
using Game.Services;
using Ships;
using Ships.Presentation;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Game-tier presentation composition root. While installed, it attaches each active ship's visual
    /// rig (declared via <see cref="ShipVisualBinding"/>) and injects a <see cref="ShipView"/>, and
    /// tears the rig down when the ship leaves play. It is owned by the game manager and gated like the
    /// player/UI: a headless/RL session simply never installs it, so its ships stay renderer-, audio-
    /// and particle-free while remaining fully simulated.
    ///
    /// The sim layer has no dependency on this type; presentation is a one-directional overlay wired
    /// through the unit registry's add/remove signals (the same seam the observer camera rides).
    /// </summary>
    public sealed class PresentationInstaller
    {
        private IUnitService units;
        private readonly Dictionary<Ship, ShipVisualRig> rigs = new();

        /// <summary>Begin attaching rigs to current and future ships. Idempotent per install/uninstall.</summary>
        public void Install(IUnitService unitService)
        {
            if (units != null) return;
            units = unitService;

            var ships = units.ActiveRegistry.ActiveShips;
            ships.OnAdd += Attach;
            ships.OnRemove += Detach;

            // Seed rigs for ships already in play (e.g. the persistent player built before install).
            foreach (var ship in ships)
                Attach(ship);
        }

        /// <summary>Unsubscribe and destroy every attached rig.</summary>
        public void Uninstall()
        {
            if (units != null)
            {
                var ships = units.ActiveRegistry.ActiveShips;
                ships.OnAdd -= Attach;
                ships.OnRemove -= Detach;
                units = null;
            }

            foreach (var rig in rigs.Values)
                if (rig) Object.Destroy(rig.gameObject);
            rigs.Clear();
        }

        private void Attach(Ship ship)
        {
            if (!ship || rigs.ContainsKey(ship)) return;

            var binding = ship.GetComponent<ShipVisualBinding>();
            if (!binding || !binding.VisualRigPrefab) return;

            var rig = Object.Instantiate(binding.VisualRigPrefab, ship.transform);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            rig.Bind(new ShipView(
                ship.transform,
                ship.Damage,
                () => ship.Movement.CurrentCommand,
                ship.Lock));

            rigs[ship] = rig;
        }

        private void Detach(Ship ship)
        {
            if (!ship || !rigs.TryGetValue(ship, out var rig)) return;
            if (rig) Object.Destroy(rig.gameObject);
            rigs.Remove(ship);
        }
    }
}

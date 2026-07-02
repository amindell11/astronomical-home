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

        /// <summary>
        /// Attach a ship's declared visual rig under it and inject its <see cref="ShipView"/>. Returns
        /// the rig (or null if the ship declares none). Single source of truth for rig attachment,
        /// shared by the installer and tests.
        /// </summary>
        public static ShipVisualRig AttachRigTo(Ship ship)
        {
            var binding = ship ? ship.GetComponent<ShipVisualBinding>() : null;
            if (!binding || !binding.VisualRigPrefab) return null;

            var rig = Object.Instantiate(binding.VisualRigPrefab, ship.transform);
            // Neutralize the rig's local transform so its children inherit the ship root's transform
            // exactly as they did when they were direct children of the ship (the rig prefab root
            // carries the ship's authored rotation/scale, which must not be double-applied).
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.transform.localScale = Vector3.one;

            rig.Bind(new ShipView(
                ship.transform,
                ship.Damage,
                () => ship.Movement.CurrentCommand,
                ship.Lock));

            return rig;
        }

        private void Attach(Ship ship)
        {
            if (!ship || rigs.ContainsKey(ship)) return;
            var rig = AttachRigTo(ship);
            if (rig) rigs[ship] = rig;
        }

        private void Detach(Ship ship)
        {
            if (!ship || !rigs.TryGetValue(ship, out var rig)) return;
            if (rig) Object.Destroy(rig.gameObject);
            rigs.Remove(ship);
        }
    }
}

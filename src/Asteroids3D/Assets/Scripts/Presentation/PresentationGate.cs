using Game.Services;
using Ships;
using Ships.Presentation;

namespace Presentation
{
    /// <summary>
    /// Game-tier presentation policy for a headless/RL session. Ship prefabs carry their visual rig as
    /// an embedded child that is active (and self-binding) by default; this gate is installed only when
    /// presentation is off, and it disables each ship's rig so the ship stays renderer-, audio- and
    /// particle-free while remaining fully simulated. In a normal session the gate is never installed
    /// and rigs simply stay on.
    ///
    /// The sim layer has no dependency on this type; it rides the unit registry's add signal (the same
    /// seam the observer camera uses), so future ships spawned by sectors are gated too.
    /// </summary>
    public sealed class PresentationGate
    {
        private IUnitService units;

        /// <summary>Begin disabling rigs on current and future ships. Idempotent per install/uninstall.</summary>
        public void Install(IUnitService unitService)
        {
            if (units != null) return;
            units = unitService;

            var ships = units.ActiveRegistry.ActiveShips;
            ships.OnAdd += DisableRig;

            foreach (var ship in ships)
                DisableRig(ship);
        }

        /// <summary>Stop gating future ships. Already-disabled rigs stay disabled.</summary>
        public void Uninstall()
        {
            if (units == null) return;
            units.ActiveRegistry.ActiveShips.OnAdd -= DisableRig;
            units = null;
        }

        private static void DisableRig(Ship ship)
        {
            if (!ship) return;
            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            if (rig)
                rig.gameObject.SetActive(false);
        }
    }
}

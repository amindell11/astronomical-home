using System.Collections;
using Damage;
using Ships;
using Ships.Registry;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sessions;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// The one home for staging a game state through its real cause, shared by preview subjects and
    /// capture scenarios: damage goes through the ship's own damage path, a run starts in the game
    /// host's order, and a sector loads through the session. Nothing here sets a runtime component's
    /// state directly.
    /// </summary>
    public static class StagedMoments
    {
        public static void Hit(Ship ship, float amount) => ship.Damage.TakeDamage(Laser(amount));

        // The shield absorbs first, so one hit sized for both leaves the hull at the fraction.
        public static void BringHullTo(Ship ship, float fraction)
        {
            var damage = ship.Damage;
            Hit(ship, damage.Shield.CurrentValue + damage.Health.CurrentValue - fraction * damage.Health.MaxValue);
        }

        public static void Kill(Ship ship)
        {
            ship.Damage.SetInvulnerability(0f);
            for (var i = 0; i < 8 && ship.Damage.Health.CurrentValue > 0f; i++) Hit(ship, 99999f);
        }

        public static IEnumerator StartRun(Session session, PlayerRig rig, int sector)
        {
            rig.EnterSector(null);
            if (session.ActiveSector) yield return session.UnloadSector();
            rig.Park();
            rig.ApplyLoadout();
            yield return session.LoadSector(sector, rig.Player);
            rig.EnterSector(session.ActiveSector.Module<PhaseSpineModule>());
        }

        // The rig gets no spine: one handed over mid-phase surges the canopy.
        public static IEnumerator ReloadSector(Session session, PlayerRig rig, int sector)
        {
            rig.EnterSector(null);
            if (session.ActiveSector) yield return session.UnloadSector();
            yield return session.LoadSector(sector, rig.Player);
        }

        private static DamageInfo Laser(float amount) =>
            new(amount, DamageKind.Laser, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
    }
}

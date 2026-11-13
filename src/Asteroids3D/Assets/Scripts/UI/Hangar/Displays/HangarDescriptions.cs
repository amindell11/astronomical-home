using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Ships;
using Ships.Loadout;

namespace UI.Hangar.Displays
{
    public static class HangarDescriptions
    {
        public static string Describe(Ship ship) =>
            $"Hull {ship.maxHealth:0}   |   Mass {ship.mass:0}   |   Bank {ship.maxBankAngle:0} deg\n" +
            $"Engine: {ship.Engine.DisplayName}   |   {Describe(ship.Engine)}\n" +
            $"Shield: {(ship.Shield ? ship.Shield.DisplayName + "   |   " + Describe(ship.Shield) : "none")}\n" +
            $"Keystone: {ship.KeystonePrefab.DisplayName} ({ship.KeystonePrefab.Cooldown:0.#} s)";

        private static string Describe(EngineModule engine) =>
            $"Top speed {engine.maxSpeed:0}   |   Turn {engine.maxYawRate:0} deg/s   |   Thrust {engine.forwardForce:0}   |   " +
            $"Strafe {engine.maxStrafeForce:0}";

        private static string Describe(ShieldModule shield) =>
            $"Capacity {shield.maxShield:0}   |   Regen {shield.shieldRegenRate:0.#}/s after {shield.shieldRegenDelay:0.#}s";

        // Reads offered prefab assets, where Awake never runs: every weapon property used must be serialized state.
        public static string Describe(WeaponComponent weapon)
        {
            var lasers = weapon as Lasers;
            if (lasers)
            {
                var damage = lasers.FirePattern == FirePattern.Volley
                    ? $"Damage {lasers.Damage:0} per bolt   |   2 bolts per shot"
                    : $"Damage {lasers.Damage:0}";
                return $"{damage}{Rate(lasers.ShotsPerSecond)}   |   Speed {lasers.ProjectileSpeed:0}" +
                       (lasers.ShotsToOverheat is int shots ? $"   |   Overheats after {shots} shots" : "");
            }

            var railguns = weapon as Railguns;
            if (railguns)
                return $"Damage {railguns.Damage:0}   |   Range {railguns.BeamRange:0}{FullCharge(railguns.Charge)}   |   Hitscan";

            var rippers = weapon as Rippers;
            if (rippers)
                return $"Damage {rippers.Damage:0} per slug{Rate(rippers.ShotsPerSecond)}" +
                       (rippers.Rounds ? $"   |   Mag {rippers.Rounds.MaxAmmo}{Refill(rippers.Rounds)}" : "") +
                       $"   |   Speed {rippers.ProjectileSpeed:0}";

            var scatterguns = weapon as Scatterguns;
            if (scatterguns)
                return $"Damage {scatterguns.Damage:0} x {scatterguns.Pellets} pellets over {scatterguns.SpreadDegrees:0} deg{Rate(scatterguns.ShotsPerSecond)}" +
                       (scatterguns.Rounds ? $"   |   {scatterguns.Rounds.MaxAmmo} shells{Refill(scatterguns.Rounds)}" : "") +
                       $"   |   Range {scatterguns.Range:0}u   |   Knockback";

            var missiles = weapon as Missiles;
            if (missiles)
                return $"Damage {missiles.Damage:0} + {missiles.SplashDamage:0} splash" +
                       (missiles.Rounds ? $"   |   {missiles.Rounds.MaxAmmo} rounds{Refill(missiles.Rounds)}" : "") +
                       "   |   Lock-on homing   |   Direct hit skips shields";

            var grenades = weapon as Grenades;
            if (grenades)
                return $"Blast {grenades.BlastDamage:0} to {grenades.BlastRadius:0}u, hits friend and foe" +
                       (grenades.Rounds ? $"   |   {grenades.Rounds.MaxAmmo} charges{Refill(grenades.Rounds)}" : "") +
                       $"   |   Range {grenades.Range:0}u";

            return weapon.DisplayName;
        }

        private static string Rate(float? shotsPerSecond) =>
            shotsPerSecond is float rate ? $"   |   Rate {rate:0.#}/s" : "";

        private static string FullCharge(ChargeTime charge) =>
            charge ? $"   |   Full charge {charge.FullChargeTime:0.#}s" : "";

        private static string Refill(Rounds rounds) =>
            rounds.ReloadTime <= 0f ? ""
            : rounds.Refill == Rounds.RefillMode.Sequential ? $" (shared recharge {rounds.ReloadTime:0.#}s/round)"
            : rounds.Refill == Rounds.RefillMode.PerRound ? $" (regen {rounds.ReloadTime:0.#}s/round)"
            : $" (reload {rounds.ReloadTime:0.#}s)";
    }
}

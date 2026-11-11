using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Ships;
using Ships.Loadout;

namespace UI.Hangar.Displays
{
    public static class HangarDescriptions
    {
        public const string Separator = " · ";

        public static string Describe(Ship ship) =>
            $"Hull {ship.maxHealth:0}{Separator}Mass {ship.mass:0}{Separator}Bank {ship.maxBankAngle:0}°\n" +
            $"{ship.Engine.DisplayName}{Separator}{(ship.Shield ? ship.Shield.DisplayName : "No shield")}{Separator}" +
            $"{ship.KeystonePrefab.DisplayName} {ship.KeystonePrefab.Cooldown:0.#} s";

        // Reads offered prefab assets, where Awake never runs: every weapon property used must be serialized state.
        public static string Describe(WeaponComponent weapon)
        {
            var lasers = weapon as Lasers;
            if (lasers)
            {
                var damage = lasers.FirePattern == FirePattern.Volley
                    ? $"Damage {lasers.Damage:0} per bolt{Separator}2 bolts per shot"
                    : $"Damage {lasers.Damage:0}";
                return $"{damage}{Rate(lasers.ShotsPerSecond)}{Separator}Speed {lasers.ProjectileSpeed:0}" +
                       (lasers.ShotsToOverheat is int shots ? $"{Separator}Overheats after {shots} shots" : "");
            }

            var railguns = weapon as Railguns;
            if (railguns)
                return $"Damage {railguns.Damage:0}{Separator}Range {railguns.BeamRange:0}{FullCharge(railguns.Charge)}{Separator}Hitscan";

            var rippers = weapon as Rippers;
            if (rippers)
                return $"Damage {rippers.Damage:0} per slug{Rate(rippers.ShotsPerSecond)}" +
                       (rippers.Rounds ? $"{Separator}Mag {rippers.Rounds.MaxAmmo}{Refill(rippers.Rounds)}" : "") +
                       $"{Separator}Speed {rippers.ProjectileSpeed:0}";

            var missiles = weapon as Missiles;
            if (missiles)
                return $"Damage {missiles.Damage:0} + {missiles.SplashDamage:0} splash" +
                       (missiles.Rounds ? $"{Separator}{missiles.Rounds.MaxAmmo} rounds{Refill(missiles.Rounds)}" : "") +
                       $"{Separator}Lock-on homing{Separator}Direct hit skips shields";

            var grenades = weapon as Grenades;
            if (grenades)
                return $"Blast {grenades.BlastDamage:0} to {grenades.BlastRadius:0}u, hits friend and foe" +
                       (grenades.Rounds ? $"{Separator}{grenades.Rounds.MaxAmmo} charges{Refill(grenades.Rounds)}" : "") +
                       $"{Separator}Range {grenades.Range:0}u";

            return weapon.DisplayName;
        }

        private static string Rate(float? shotsPerSecond) =>
            shotsPerSecond is float rate ? $"{Separator}Rate {rate:0.#}/s" : "";

        private static string FullCharge(ChargeTime charge) =>
            charge ? $"{Separator}Full charge {charge.FullChargeTime:0.#}s" : "";

        private static string Refill(Rounds rounds) =>
            rounds.ReloadTime <= 0f ? ""
            : rounds.Refill == Rounds.RefillMode.Sequential ? $" (shared recharge {rounds.ReloadTime:0.#}s/round)"
            : rounds.Refill == Rounds.RefillMode.PerRound ? $" (regen {rounds.ReloadTime:0.#}s/round)"
            : $" (reload {rounds.ReloadTime:0.#}s)";
    }
}

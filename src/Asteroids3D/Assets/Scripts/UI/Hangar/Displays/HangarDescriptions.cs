using System;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Ships;

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
        public static (string number, string caption) Damage(WeaponComponent weapon)
        {
            var lasers = weapon as Lasers;
            if (lasers)
                return ($"{lasers.Damage:0}",
                    lasers.FirePattern == FirePattern.Volley ? "per bolt, 2 per shot" : "per bolt");

            var railguns = weapon as Railguns;
            if (railguns)
                return ($"{railguns.Damage:0}", "per beam");

            var rippers = weapon as Rippers;
            if (rippers)
                return ($"{rippers.Damage:0}", "per slug");

            var scatterguns = weapon as Scatterguns;
            if (scatterguns)
                return ($"{scatterguns.Damage:0}", $"per pellet, {scatterguns.Pellets} per shot");

            var missiles = weapon as Missiles;
            if (missiles)
                return ($"{missiles.Damage:0}", $"direct hit, +{missiles.SplashDamage:0} splash");

            var grenades = weapon as Grenades;
            if (grenades)
                return ($"{grenades.BlastDamage:0}", "blast");

            throw new InvalidOperationException(
                $"The hangar has no damage readout for {weapon.DisplayName} ({weapon.GetType().Name}).");
        }
    }
}

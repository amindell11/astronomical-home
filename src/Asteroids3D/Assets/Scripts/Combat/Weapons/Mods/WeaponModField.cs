namespace Combat.Weapons.Mods
{
    // Values are pinned: mod assets serialize the numbers, so renumbering rewrites authored data.
    /// <summary>
    /// A weapon field a <see cref="WeaponMod"/> changes. Rate fields are authored as seconds and read
    /// divided, so a bonus means more per second; Magazine adds whole rounds; the rest scale up.
    /// </summary>
    public enum WeaponModField
    {
        FireRate = 0,
        Damage = 1,
        HeatCapacity = 2,
        Cooling = 3,
        Magazine = 4,
        ReloadSpeed = 5,
        ChargeSpeed = 6,
        BlastRadius = 7,
    }

    public static class WeaponModFields
    {
        public const int Count = 8;

        /// <summary>Authored as seconds: a bonus divides the authored time.</summary>
        public static bool IsRate(this WeaponModField field) =>
            field is WeaponModField.FireRate or WeaponModField.ReloadSpeed or WeaponModField.ChargeSpeed;

        public static string Label(this WeaponModField field) => field switch
        {
            WeaponModField.FireRate => "fire rate",
            WeaponModField.Damage => "damage",
            WeaponModField.HeatCapacity => "heat capacity",
            WeaponModField.Cooling => "cooling",
            WeaponModField.Magazine => "magazine",
            WeaponModField.ReloadSpeed => "reload speed",
            WeaponModField.ChargeSpeed => "charge speed",
            WeaponModField.BlastRadius => "blast radius",
            _ => field.ToString(),
        };
    }
}

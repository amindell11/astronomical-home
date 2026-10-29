namespace Ships.Mods
{
    // Values are pinned: mod assets serialize the numbers, so renumbering rewrites authored data.
    /// <summary>
    /// A field group a <see cref="ShipMod"/> changes, grouped as the keystone scales group handling
    /// (speed, thrust, maneuverability). ShieldRecharge is the regen delay, authored as seconds and
    /// read divided, so a bonus means a shorter wait; the rest scale up.
    /// </summary>
    public enum ShipModField
    {
        Hull = 0,
        Speed = 1,
        Thrust = 2,
        Handling = 3,
        ShieldCapacity = 4,
        ShieldRegen = 5,
        ShieldRecharge = 6,
    }

    /// <summary>What a field group belongs to: the hull itself, or one of its modules.</summary>
    public enum ShipPart
    {
        Hull,
        Engine,
        Shield,
    }

    public static class ShipModFields
    {
        public const int Count = 7;

        /// <summary>Authored as seconds: a bonus divides the authored time.</summary>
        public static bool IsRate(this ShipModField field) => field == ShipModField.ShieldRecharge;

        public static ShipPart Part(this ShipModField field) => field switch
        {
            ShipModField.Hull => ShipPart.Hull,
            ShipModField.Speed or ShipModField.Thrust or ShipModField.Handling => ShipPart.Engine,
            _ => ShipPart.Shield,
        };

        public static string Label(this ShipModField field) => field switch
        {
            ShipModField.Hull => "max hull",
            ShipModField.Speed => "max speed",
            ShipModField.Thrust => "thrust",
            ShipModField.Handling => "handling",
            ShipModField.ShieldCapacity => "shield capacity",
            ShipModField.ShieldRegen => "shield regen",
            ShipModField.ShieldRecharge => "shield recharge",
            _ => field.ToString(),
        };
    }
}

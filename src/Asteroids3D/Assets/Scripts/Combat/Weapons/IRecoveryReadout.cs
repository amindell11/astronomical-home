using System;

namespace Combat.Weapons
{
    /// <summary>A weapon that comes back from a recovery: overheat, a reload, a regained round or a recovery cooldown.</summary>
    public interface IRecoveryReadout : IWeaponReadout
    {
        event Action Recovered;
    }
}

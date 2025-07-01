using UnityEngine;

/// <summary>
/// Simple interface abstraction for any ship weapon that can be triggered via AI.
/// </summary>
public interface IWeapon
{
    /// <summary>Attempt to fire the weapon (may internally honour cooldown).</summary>
    /// <returns>True if a shot was fired, false otherwise.</returns>
    bool Fire();

    /// <summary>
    /// Checks if the weapon can be fired, considering ammo, heat, etc.
    /// Does not consider fire-rate cooldown.
    /// </summary>
    bool CanFire();
}
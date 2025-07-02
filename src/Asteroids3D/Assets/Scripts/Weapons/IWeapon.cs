using UnityEngine;

/// <summary>
/// Simple interface abstraction for any ship weapon that can be triggered via AI.
/// </summary>
public interface IWeapon
{
    /// <summary>Attempt to fire the weapon (may internally honour cooldown).</summary>
    /// <returns>The projectile instance that was fired, or null if no shot was fired.</returns>
    ProjectileBase Fire();

    /// <summary>
    /// Checks if the weapon can be fired, considering ammo, heat, etc.
    /// Does not consider fire-rate cooldown.
    /// </summary>
    bool CanFire();
}
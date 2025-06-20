using UnityEngine;

/// <summary>
/// Simple interface abstraction for any ship weapon that can be triggered via AI.
/// </summary>
public interface IWeapon
{
    /// <summary>Attempt to fire the weapon (may internally honour cooldown).</summary>
    void Fire();
}
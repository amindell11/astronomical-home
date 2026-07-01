using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// Shared damage helpers for PlayMode tests. Centralizes the "kill this ship" pattern
/// that was previously re-inlined per test (deal shield-depleting then lethal hull damage).
/// </summary>
public static class TestDamage
{
    /// <summary>
    /// Kills a ship deterministically. A single <see cref="Ships.Damage.DamageController.TakeDamage"/>
    /// call drains EITHER shield OR hull (damage does not overflow shield→hull by design), so this
    /// clears invulnerability and hits repeatedly until health reaches zero.
    /// </summary>
    /// <param name="ship">Ship to kill (no-op if null).</param>
    /// <param name="instigator">
    /// Optional attacker GameObject; pass an enemy ship's GameObject when the test asserts on
    /// kill attribution (LastAttackerId / OnDeath killer). Null leaves the attacker unset.
    /// </param>
    public static void Kill(Ship ship, GameObject instigator = null)
    {
        if (ship == null) return;

        ship.Damage.SetInvulnerability(0f);
        for (var i = 0; i < 8 && ship.Damage.Health.CurrentValue > 0f; i++)
            ship.Damage.TakeDamage(99999f, 0f, Vector3.zero, Vector3.zero, instigator);
    }
}

} // namespace Tests.PlayMode.Common

using Damage;
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
    /// Kills a ship deterministically; the loop covers damage rules where a single hit cannot kill.
    /// </summary>
    /// <param name="ship">Ship to kill (no-op if null).</param>
    /// <param name="instigator">
    /// Optional attacker; pass an enemy ship when the test asserts on kill attribution
    /// (OnDeath killing blow). Null leaves the attacker invalid.
    /// </param>
    public static void Kill(Ship ship, Ship instigator = null)
    {
        if (ship == null) return;

        ship.Damage.SetInvulnerability(0f);
        var hit = new DamageInfo(99999f, DamageKind.Laser,
            instigator ? instigator.Id : ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
        for (var i = 0; i < 8 && ship.Damage.Health.CurrentValue > 0f; i++)
            ship.Damage.TakeDamage(hit);
    }
}

} // namespace Tests.PlayMode.Common

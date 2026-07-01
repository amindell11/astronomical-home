using System;
using Damage;
using UnityEngine;

namespace Ships.Damage
{
    /// <summary>
    /// The push-facing damage surface that presentation binds to (flashes, sparks, smoke, death VFX,
    /// SFX). Satisfied as-is by <see cref="DamageController"/>. Deliberately kept separate from the
    /// commander's polled <see cref="Ships.Command.IShipStatus"/> so neither consumer sees the other's
    /// surface — the same role-scoping principle as <see cref="Ships.Command.ShipControl"/>.
    /// </summary>
    public interface IDamageEvents
    {
        /// <summary>Applied damage amount and world hit point.</summary>
        event Action<float, Vector3> OnDamaged;

        /// <summary>Victim id, killer id.</summary>
        event Action<ShipId, ShipId> OnDeath;

        Resource Health { get; }
        RegenResource Shield { get; }
    }
}

using System;
using Damage;
using UnityEngine;
using Ships.Registry;

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
        /// <summary>Per-hit context; <see cref="DamageInfo.Amount"/> is the applied damage (absorbed by shield + hull).</summary>
        event Action<DamageInfo> OnDamaged;

        /// <summary>Victim id and the killing blow. Fires once per life.</summary>
        event Action<ShipId, DamageInfo> OnDeath;

        Resource Health { get; }
        RegenResource Shield { get; }
    }
}

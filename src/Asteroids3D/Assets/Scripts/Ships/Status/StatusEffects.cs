using System;
using UnityEngine;

namespace Ships.Status
{
    // Values index the timers; append new effects at the end.
    public enum StatusEffect
    {
        Stunned = 0,
        WeaponsJammed = 1,
        EnginesOut = 2,
    }

    /// <summary>
    /// Timed effects other ships put on this one. Movement, the weapons and the keystone read the
    /// Can* gates; the ship advances the clock each fixed step, never <c>Time.time</c>, so effects
    /// replay identically across arenas like the keystone's cooldown. Re-applying an effect keeps
    /// whichever timer ends later.
    /// </summary>
    public sealed class StatusEffects
    {
        private const int Count = 3;

        private readonly float[] endsAt = new float[Count];
        private float clock;

        public bool Has(StatusEffect effect) => clock < endsAt[(int)effect];

        public float Remaining(StatusEffect effect) => Mathf.Max(0f, endsAt[(int)effect] - clock);

        public bool CanThrust => !Has(StatusEffect.Stunned) && !Has(StatusEffect.EnginesOut);

        public bool CanTurn => !Has(StatusEffect.Stunned);

        public bool CanFire => !Has(StatusEffect.Stunned) && !Has(StatusEffect.WeaponsJammed);

        public bool CanLock => CanFire;

        public bool CanKeystone => !Has(StatusEffect.Stunned);

        public void Apply(StatusEffect effect, float seconds)
        {
            if (!(seconds > 0f))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, $"A {effect} effect lasts a positive time.");
            var index = (int)effect;
            endsAt[index] = Mathf.Max(endsAt[index], clock + seconds);
        }

        public void Tick(float dt) => clock += dt;

        public void Clear()
        {
            clock = 0f;
            Array.Clear(endsAt, 0, Count);
        }
    }
}

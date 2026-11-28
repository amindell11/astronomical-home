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
        Suppressed = 3,
        Stalled = 4,
        Exposed = 5,
    }

    /// <summary>
    /// Timed effects other ships put on this one. Movement, the weapons and the keystone read the
    /// Can* gates; the ship advances the clock each fixed step, never <c>Time.time</c>, so effects
    /// replay identically across arenas like the keystone's cooldown. Re-applying an effect keeps
    /// whichever timer ends later. A diminishing effect remembers its last application, so a repeat
    /// within the window lasts less.
    /// </summary>
    public sealed class StatusEffects
    {
        private const int Count = 6;

        private readonly float[] endsAt = new float[Count];
        private readonly float[] lastApplied = new float[Count];
        private float clock;

        public StatusEffects()
        {
            ForgetApplications();
        }

        public bool Has(StatusEffect effect) => clock < endsAt[(int)effect];

        public float Remaining(StatusEffect effect) => Mathf.Max(0f, endsAt[(int)effect] - clock);

        public bool CanThrust => !Has(StatusEffect.Stunned) && !Has(StatusEffect.EnginesOut) && !Has(StatusEffect.Stalled);

        public bool CanTurn => !Has(StatusEffect.Stunned) && !Has(StatusEffect.Stalled);

        public bool CanFire => !Has(StatusEffect.Stunned) && !Has(StatusEffect.WeaponsJammed);

        public bool CanLock => CanFire;

        public bool CanKeystone => !Has(StatusEffect.Stunned) && !Has(StatusEffect.Suppressed) && !Has(StatusEffect.Stalled);

        public bool CanRegenShield => !Has(StatusEffect.Stalled);

        public bool Slowed => Has(StatusEffect.Suppressed);

        public void Apply(StatusEffect effect, float seconds)
        {
            if (!(seconds > 0f))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, $"A {effect} effect lasts a positive time.");
            var index = (int)effect;
            endsAt[index] = Mathf.Max(endsAt[index], clock + seconds);
        }

        public void ApplyDiminishing(StatusEffect effect, float seconds, float repeatWindow, float repeatScale)
        {
            var index = (int)effect;
            var repeat = clock - lastApplied[index] < repeatWindow;
            lastApplied[index] = clock;
            Apply(effect, repeat ? seconds * repeatScale : seconds);
        }

        public void Tick(float dt) => clock += dt;

        public void Clear()
        {
            clock = 0f;
            Array.Clear(endsAt, 0, Count);
            ForgetApplications();
        }

        private void ForgetApplications()
        {
            for (var i = 0; i < Count; i++)
                lastApplied[i] = float.NegativeInfinity;
        }
    }
}

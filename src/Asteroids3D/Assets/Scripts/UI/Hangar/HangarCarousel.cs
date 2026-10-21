using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// The hangar preview's carousel: position p (unwrapped ship indices) chases the selected index t on a
    /// critically damped spring. A press moves t and keeps p's velocity, so rapid presses stream ships along
    /// one path instead of restarting it. One press replays the formation handoff exactly: an offset maps back
    /// to the handoff clock through the spring's own single-step response.
    /// </summary>
    internal sealed class HangarCarousel
    {
        // Rate × handoff duration: one press settles to within 1.7% of an index by the handoff's end.
        private const float SettleRate = 6f;
        private const float Rate = SettleRate / HangarPreviewFlight.HandoffSeconds;
        private const float MaxSpeed = 8f;
        private const int MaxLead = 2;

        internal static readonly float SettledOffset = 1f - Response(SettleRate);

        private readonly Dictionary<int, Ship> slots = new();

        public float Position { get; private set; }
        public float Velocity { get; private set; }
        public int Target { get; private set; }

        public void Start(Ship ship)
        {
            slots.Clear();
            Position = Velocity = Target = 0;
            slots[0] = ship;
        }

        /// <summary>Retargets one index toward <paramref name="direction"/>; false when the pick is unchanged.</summary>
        public bool Step(Ship ship, int direction)
        {
            if (slots[Target] == ship) return false;
            Target += direction;
            slots[Target] = ship;
            return true;
        }

        public Ship At(int index) => slots.TryGetValue(index, out var ship) ? ship : null;

        public void Advance(float dt)
        {
            // Exact critically damped step: stable at any frame time.
            var offset = Position - Target;
            var decay = Mathf.Exp(-Rate * dt);
            var drive = (Velocity + Rate * offset) * dt;
            Position = Target + (offset + drive) * decay;
            Velocity = Mathf.Clamp((Velocity - Rate * drive) * decay, -MaxSpeed, MaxSpeed);

            var lead = Target - Position;
            if (Mathf.Abs(lead) > MaxLead)
                Position += Mathf.Sign(lead) * Mathf.Ceil(Mathf.Abs(lead) - MaxLead);
            if (Mathf.Abs(lead) < 1e-4f && Mathf.Abs(Velocity) < 1e-3f)
            {
                Position = Target;
                Velocity = 0f;
            }

            foreach (var index in new List<int>(slots.Keys))
                if (index != Target && Mathf.Abs(index - Position) > MaxLead + 1)
                    slots.Remove(index);
        }

        /// <summary>Handoff clock (0–1) at which one press has carried p <paramref name="progress"/> of an index.</summary>
        internal static float HandoffTime(float progress)
        {
            if (progress >= 1f - SettledOffset) return 1f;
            float lo = 0f, hi = SettleRate;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * .5f;
                if (Response(mid) < progress) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * .5f / SettleRate;
        }

        // Unit step response of the critically damped spring at x = rate × time.
        private static float Response(float x) => 1f - (1f + x) * Mathf.Exp(-x);
    }
}

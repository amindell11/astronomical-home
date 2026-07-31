using System;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The one sim-semantics rule for all RL runs: every rendered frame advances exactly one fixed step, so Update-driven systems (Scout scanning, shield regen) tick once per fixed step at any speed. The trainer's engine-config defaults (time_scale 20, capture_frame_rate 60) violate this at the 50 Hz fixed step — the YAML must pin engine_settings and the host asserts the resolved Time settings.</summary>
    public static class PacingContract
    {
        public static void Apply()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;
        }

        public static void AssertHolds()
        {
            var simPerFrame = Time.captureDeltaTime * Time.timeScale;
            if (Mathf.Abs(simPerFrame - Time.fixedDeltaTime) > 1e-6f)
                throw new InvalidOperationException(
                    $"Pacing contract violated: captureDeltaTime ({Time.captureDeltaTime}) × timeScale ({Time.timeScale}) "
                    + $"must equal fixedDeltaTime ({Time.fixedDeltaTime}). Pin engine_settings in the trainer YAML "
                    + $"(time_scale: 1, capture_frame_rate: {Mathf.RoundToInt(1f / Time.fixedDeltaTime)}).");
        }
    }
}

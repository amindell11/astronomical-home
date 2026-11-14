using System;
using UnityEngine;

namespace RL.Hosts
{
    /// <summary>The one sim-semantics rule for harness runs: every rendered frame advances exactly one fixed step, so Update-driven systems (Scout scanning, shield regen) tick once per fixed step at any speed. The host applies it and asserts the resolved Time settings.</summary>
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
                    + $"must equal fixedDeltaTime ({Time.fixedDeltaTime}). Something changed Time after PacingContract.Apply.");
        }
    }
}

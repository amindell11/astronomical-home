using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Recap
{
    /// <summary>
    /// The game's playback of a recap sequence: timed spans advance on the unscaled clock a frame at a
    /// time, and a hold waits on what releases it, the player's death effects, a fresh press, Continue.
    /// </summary>
    public static class RecapPlayback
    {
        // Continue invoked early ends the playback at once, as the old recap did.
        public static IEnumerator Run(RecapOutcomeSequence sequence, Action<RecapState> apply,
            Func<bool> deathEffectsPlaying, Func<bool> continued)
        {
            for (var i = 0; i < sequence.SpanCount && !continued(); i++)
            {
                var span = sequence.Span(i);
                for (var t = 0f; t < span.Seconds && !continued(); t += Time.unscaledDeltaTime)
                {
                    apply(sequence.StateAt(i, t));
                    yield return null;
                }
                if (continued()) yield break;
                apply(sequence.StateAt(i, span.Seconds));
                switch (span.Hold)
                {
                    case RecapHold.DeathEffects:
                        while (deathEffectsPlaying() && !continued()) yield return null;
                        break;
                    case RecapHold.Press:
                        yield return WaitForPress(continued);
                        break;
                    case RecapHold.Continue:
                        while (!continued()) yield return null;
                        break;
                }
            }
        }

        // A key held from before the banner reads unpressed until pressed afresh (no initial state check).
        private static IEnumerator WaitForPress(Func<bool> continued)
        {
            using var advance = new InputAction("Continue", InputActionType.Button, "<Keyboard>/anyKey");
            advance.AddBinding("<Mouse>/leftButton");
            advance.AddBinding("<Gamepad>/buttonSouth");
            var requested = false;
            advance.performed += _ => requested = true;
            advance.Enable();
            while (!requested && !continued()) yield return null;
            advance.Disable();
        }
    }
}

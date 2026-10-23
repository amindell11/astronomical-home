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
                var held = sequence.StateAt(i, span.Seconds);
                apply(held);
                switch (span.Hold)
                {
                    case RecapHold.DeathEffects:
                        yield return Hold(held, apply, () => !deathEffectsPlaying() || continued());
                        break;
                    case RecapHold.Press:
                        yield return WaitForPress(held, apply, continued);
                        break;
                    case RecapHold.Continue:
                        yield return Hold(held, apply, continued);
                        break;
                }
            }
        }

        // Reapplied every frame: a ship spawned mid-hold gets the held state too.
        private static IEnumerator Hold(RecapState held, Action<RecapState> apply, Func<bool> released)
        {
            while (!released())
            {
                apply(held);
                yield return null;
            }
        }

        // A key held from before the banner reads unpressed until pressed afresh (no initial state check).
        private static IEnumerator WaitForPress(RecapState held, Action<RecapState> apply, Func<bool> continued)
        {
            using var advance = new InputAction("Continue", InputActionType.Button, "<Keyboard>/anyKey");
            advance.AddBinding("<Mouse>/leftButton");
            advance.AddBinding("<Gamepad>/buttonSouth");
            var requested = false;
            advance.performed += _ => requested = true;
            advance.Enable();
            yield return Hold(held, apply, () => requested || continued());
            advance.Disable();
        }
    }
}

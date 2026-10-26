using System;
using System.Collections;
using UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Recap
{
    /// <summary>
    /// The game's playback of a recap sequence: timed spans advance on the unscaled clock a frame at a
    /// time, and input holds consume a fresh press and its release before enabling Continue.
    /// RunScreen owns only the screen; callers supply any world presentation and clock changes.
    /// </summary>
    public static class RecapPlayback
    {
        public static IEnumerator RunScreen(RecapOutcomeSequence sequence, Transform parent,
            Action<RecapState> applyWorld = null, Func<bool> cancelled = null)
        {
            var screen = RunRecapScreen.Create(parent, sequence.Context);
            try
            {
                yield return Run(sequence, state =>
                {
                    screen.Apply(state);
                    applyWorld?.Invoke(state);
                }, () => screen.Continued || cancelled?.Invoke() == true);
            }
            finally { UnityEngine.Object.Destroy(screen.gameObject); }
        }

        public static IEnumerator Run(RecapOutcomeSequence sequence, Action<RecapState> apply,
            Func<bool> continued)
        {
            using var advance = new InputAction("Continue", InputActionType.Button, "<Keyboard>/anyKey");
            advance.AddBinding("<Mouse>/leftButton");
            advance.AddBinding("<Gamepad>/buttonSouth");
            var requested = false;
            advance.performed += _ => requested = true;
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
                    case RecapHold.Press:
                        advance.Enable();
                        yield return Hold(held, apply, () => requested || continued());
                        break;
                    case RecapHold.Release:
                        yield return Hold(held, apply, () => !advance.IsPressed() || continued());
                        advance.Disable();
                        break;
                    case RecapHold.Continue:
                        yield return Hold(held, apply, continued);
                        break;
                }
            }
        }

        // Newly spawned ships receive the held presentation state too.
        private static IEnumerator Hold(RecapState held, Action<RecapState> apply, Func<bool> released)
        {
            while (!released())
            {
                apply(held);
                yield return null;
            }
        }
    }
}

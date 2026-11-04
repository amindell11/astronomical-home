using System;
using System.Linq;
using Game.Moments;
using UI.Comms;
using UnityEngine;

namespace Game.Companion
{
    /// <summary>
    /// Speaks for the run's moments: each raised moment picks a line from its line slot, fills in the
    /// moment's facts and offers it to the nag budget, and a line the budget lets through shows on the comm
    /// line. Built once per session; each run hands it that run's moment stream. With debug labels on, an
    /// empty line slot speaks a bracketed label of its id and the moment's facts instead of staying silent.
    /// </summary>
    public sealed class CompanionVoice : MonoBehaviour
    {
        private CompanionLineSheet sheet;
        private CompanionFeelProfile feel;
        private CommLine display;
        private Func<bool> canSpeak;
        private bool debugLabels;
        private NagBudget budget;
        private RunMomentStream moments;

        public static CompanionVoice Create(Transform parent, CompanionLineSheet sheet, CompanionFeelProfile feel,
            CommLine display, Func<bool> canSpeak, bool debugLabels)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!sheet) throw new ArgumentNullException(nameof(sheet));
            if (!feel) throw new ArgumentNullException(nameof(feel));
            if (!display) throw new ArgumentNullException(nameof(display));
            if (canSpeak == null) throw new ArgumentNullException(nameof(canSpeak));

            var go = new GameObject("Companion");
            go.transform.SetParent(parent, false);
            var voice = go.AddComponent<CompanionVoice>();
            voice.sheet = sheet;
            voice.feel = feel;
            voice.display = display;
            voice.canSpeak = canSpeak;
            voice.debugLabels = debugLabels;
            voice.budget = new NagBudget(feel);
            return voice;
        }

        public void Initialize(RunMomentStream moments)
        {
            if (moments == null) throw new ArgumentNullException(nameof(moments));
            if (this.moments != null) this.moments.Raised -= OnRaised;
            this.moments = moments;
            moments.Raised += OnRaised;
        }

        private void OnRaised(IRunMoment moment)
        {
            var slot = sheet.Find(moment.Slot);
            var line = slot.Pick(n => UnityEngine.Random.Range(0, n));
            if (line != null)
                foreach (var fact in moment.Facts)
                    line = line.Replace("{" + fact.Key + "}", fact.Value);
            else if (debugLabels)
                line = DebugLabel(moment);
            else
                return;
            budget.Offer(line, slot.MustSay, Time.unscaledTime);
            TrySpeak();
        }

        private static string DebugLabel(IRunMoment moment) =>
            ("[" + moment.Slot + "] " + string.Join(" · ", moment.Facts.Where(fact => fact.Value.Length > 0)
                .Select(fact => fact.Key + " " + fact.Value))).TrimEnd();

        private void Update() => TrySpeak();

        private void TrySpeak()
        {
            if (budget.TryTake(Time.unscaledTime, canSpeak(), out var line))
                display.Show(line, feel.HoldSeconds, feel.FadeSeconds);
        }

        private void OnDestroy()
        {
            if (moments != null) moments.Raised -= OnRaised;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Moments;
using Substrate.Sectors.Factions;
using UI.Comms;
using UnityEngine;

namespace Game.Companion
{
    /// <summary>
    /// Speaks for the run's moments. A moment picks an exchange, and the reply shows when the opener's
    /// hold ends. The exchange comes from the line file's rows for the moment's line slot and faction;
    /// each line is filled from the facts, prefixed with who speaks it in that speaker's colour (the
    /// moment's faction colour for an enemy), and offered to the nag budget.
    /// A line the budget lets through shows on the comm line. Built once per session; each run hands it
    /// the moment stream. With debug labels on, a moment with no matching line speaks a bracketed label
    /// of its id and facts instead of staying silent, prefixed the same way.
    /// </summary>
    public sealed class CompanionVoice : MonoBehaviour
    {
        private CompanionLineSheet sheet;
        private LineTable lines;
        private CompanionFeelProfile feel;
        private CommLine display;
        private Func<bool> canSpeak;
        private bool debugLabels;
        private NagBudget budget;
        private RunMomentStream moments;
        private Dictionary<string, Color> factionColors;

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
            voice.lines = sheet.LoadLines();
            voice.feel = feel;
            voice.display = display;
            voice.canSpeak = canSpeak;
            voice.debugLabels = debugLabels;
            voice.budget = new NagBudget(feel);
            return voice;
        }

        public void Initialize(RunMomentStream moments, IReadOnlyList<Faction> factions)
        {
            if (moments == null) throw new ArgumentNullException(nameof(moments));
            if (factions == null) throw new ArgumentNullException(nameof(factions));
            factionColors = new Dictionary<string, Color>();
            for (var i = 0; i < factions.Count; i++)
            {
                if (!factions[i]) throw new ArgumentException($"Faction {i} is null.", nameof(factions));
                factionColors[factions[i].id] = factions[i].color;
            }

            if (this.moments != null) this.moments.Raised -= OnRaised;
            this.moments = moments;
            moments.Raised += OnRaised;
        }

        private void OnRaised(IRunMoment moment)
        {
            var slot = sheet.Find(moment.Slot);
            var picked = lines.Pick(moment.Slot, moment.Facts, n => UnityEngine.Random.Range(0, n),
                UnityEngine.Random.value < feel.ReplyChance);
            if (picked.Count == 0)
            {
                if (!debugLabels) return;
                var speaker = slot.SpokenBy;
                budget.Offer(new CommMessage(speaker, SpeakerColor(speaker, slot, moment), DebugLabel(moment)),
                    slot.MustSay, Time.unscaledTime, null);
                TrySpeak();
                return;
            }

            var first = Spoken(picked[0], slot, moment);
            var second = picked.Count > 1 ? Spoken(picked[1], slot, moment) : (CommMessage?)null;
            budget.Offer(first, slot.MustSay, Time.unscaledTime, second);
            TrySpeak();
        }

        private CommMessage Spoken(SpokenLine line, LineSlot slot, IRunMoment moment)
        {
            var text = line.Text;
            foreach (var fact in moment.Facts)
                text = text.Replace("{" + fact.Key + "}", fact.Value);
            return new CommMessage(line.Speaker, SpeakerColor(line.Speaker, slot, moment), text);
        }

        private Color SpeakerColor(string speaker, LineSlot slot, IRunMoment moment)
        {
            if (speaker == LineTable.CompanionSpeaker) return feel.CompanionColor;
            if (!string.IsNullOrWhiteSpace(slot.Speaker) && speaker == slot.Speaker) return feel.NeutralSpeakerColor;
            return moment.Facts.TryGetValue("faction", out var faction) && factionColors.TryGetValue(faction, out var color)
                ? color
                : feel.NeutralSpeakerColor;
        }

        private static string DebugLabel(IRunMoment moment) =>
            ("[" + moment.Slot + "] " + string.Join(" · ", moment.Facts.Where(fact => fact.Value.Length > 0)
                .Select(fact => fact.Key + " " + fact.Value))).TrimEnd();

        private void Update() => TrySpeak();

        private void TrySpeak()
        {
            if (budget.TryTake(Time.unscaledTime, canSpeak(), out var line))
                display.Show(line, feel.HoldFor(line.Plain), feel.FadeSeconds);
        }

        private void OnDestroy()
        {
            if (moments != null) moments.Raised -= OnRaised;
        }
    }
}

using System.Collections.Generic;
using Substrate.Sectors.Elements.Hunt;
using Ships;
using UI;
using UI.Prompts;
using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// The guided first sector, one step at a time: movement prompts, then the weapon and keystone prompts, then the
    /// hunt intro, then the waves card at the first alarm. Each prompt step ends when the player has used every key it
    /// shows. The host holds the hunt from the start, runs the intro's camera move when <see cref="IntroDue"/>, and
    /// resumes the hunt after it. The design and rulings: https://github.com/amindell11/astronomical-home/issues/1761
    /// </summary>
    internal sealed class FirstSectorTutorial
    {
        public const string IntroText =
            "EXPLORE THE SECTOR AND LOOK FOR UPGRADES. ENEMIES ARE PATROLLING AND CAN SENSE YOU, SO STAY ON THE LOOKOUT.";
        public const string WavesText = "WAVES OF ENEMIES ARE COMING. SURVIVE THEM AND REACH EXTRACTION.";

        private enum Step { Movement, Weapons, Intro, Hunting, Done }

        private readonly Overlay overlay;
        private readonly SectorHunt hunt;
        private readonly float wavesCardSeconds;
        private IReadOnlyList<FirstRunPrompt> lines;
        private Step step;
        private bool ended;

        public FirstSectorTutorial(Overlay overlay, SectorHunt hunt, float wavesCardSeconds)
        {
            this.overlay = overlay;
            this.hunt = hunt;
            this.wavesCardSeconds = wavesCardSeconds;
            lines = overlay.ShowMovementPrompts(() => ended);
        }

        public bool IntroDue => step == Step.Intro;

        public Ship Patrol
        {
            get
            {
                foreach (var ship in hunt.Spawned)
                    if (ship && ship.gameObject.activeInHierarchy) return ship;
                return null;
            }
        }

        public void Tick()
        {
            switch (step)
            {
                case Step.Movement when Gone(lines):
                    lines = overlay.ShowFirstRunPrompts(() => ended);
                    step = Step.Weapons;
                    break;
                case Step.Weapons when Gone(lines):
                    lines = null;
                    step = Step.Intro;
                    break;
                case Step.Hunting when hunt.Alarmed:
                    var until = Time.unscaledTime + wavesCardSeconds;
                    overlay.ShowTutorialCard(WavesText, () => ended || Time.unscaledTime >= until);
                    step = Step.Done;
                    break;
            }
        }

        public void IntroDone() => step = Step.Hunting;

        public void End() => ended = true;

        private static bool Gone(IReadOnlyList<FirstRunPrompt> shown)
        {
            foreach (var line in shown)
                if (line) return false;
            return true;
        }
    }
}

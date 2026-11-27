using System;
using System.Collections.Generic;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Hunt;
using Ships;
using UI;
using UI.Prompts;
using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// The guided first sector, one step at a time: movement prompts, then the weapon and keystone prompts, then the
    /// hunt intro (the stations, then a patrol), then the waves card at the first alarm. Each prompt step ends when the player has used every key it
    /// shows. The host holds the hunt from the start, runs the intro's camera move when <see cref="IntroDue"/>, and
    /// resumes the hunt after it. The design and rulings: https://github.com/amindell11/astronomical-home/issues/1761
    /// </summary>
    internal sealed class FirstSectorTutorial
    {
        public static string StationsText(IReadOnlyList<StationKind> kinds)
        {
            if (kinds == null || kinds.Count == 0)
                throw new ArgumentException("The stations card needs at least one kind.", nameof(kinds));
            var parts = new List<string>();
            foreach (var kind in kinds) parts.Add(Does(kind));
            var list = parts.Count == 1
                ? parts[0]
                : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " AND " + parts[parts.Count - 1];
            return $"STATIONS ARE MARKED ON YOUR MAP. {list}. FLY INTO ONE TO USE IT BEFORE THE ALARM SOUNDS.";
        }

        public const string IntroText = "ENEMIES ARE PATROLLING AND CAN SENSE YOU, SO STAY ON THE LOOKOUT.";
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
                    step = Step.Intro;
                    break;
                case Step.Hunting when hunt.Alarmed:
                    var until = Time.unscaledTime + wavesCardSeconds;
                    overlay.ShowTutorialCard(WavesText, TutorialCard.Spot.UnderBanner, () => ended || Time.unscaledTime >= until);
                    step = Step.Done;
                    break;
            }
        }

        public void IntroDone() => step = Step.Hunting;

        public void End() => ended = true;

        private static string Does(StationKind kind) => kind switch
        {
            StationKind.Repair => "REPAIR FIXES YOUR HULL",
            StationKind.Mod => "MOD ADDS AN UPGRADE",
            StationKind.Armory => "ARMORY SWAPS YOUR WEAPON",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A station kind with no tutorial line."),
        };

        private static bool Gone(IReadOnlyList<FirstRunPrompt> shown)
        {
            foreach (var line in shown)
                if (line) return false;
            return true;
        }
    }
}

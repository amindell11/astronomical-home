using System.Collections;
using UnityEngine;
using RL.Reward;

namespace RL.Episodes
{
    /// <summary>The episode ordering contract, in one place: pair-reset to the episode's poses, a fresh <see cref="EpisodeRunner"/>, then one Tick per fixed step until the runner ends the episode. The composition installs the brains before the reset, because the respawn re-inits the installed brain.</summary>
    public sealed class EpisodeLoopDriver
    {
        private readonly EpisodePair pair;
        private readonly Vector2 arenaCenter;
        private readonly WaitForFixedUpdate waitFixed = new();

        public EpisodeRunner Runner { get; private set; }

        public EpisodeLoopDriver(EpisodePair pair, Vector2 arenaCenter)
        {
            this.pair = pair;
            this.arenaCenter = arenaCenter;
        }

        /// <summary><paramref name="onBegin"/> fires once after Begin (spawn pose settled) and <paramref name="onFixedStep"/> once per fixed step after Tick — the hooks a per-step probe rides.</summary>
        public IEnumerator RunEpisode(RewardSpec spec, int episodeIndex,
            System.Action onBegin = null, System.Action onFixedStep = null)
        {
            pair.Reset(in spec, episodeIndex);
            Runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, episodeIndex, arenaCenter);
            Runner.Begin();
            onBegin?.Invoke();

            while (!Runner.IsDone)
            {
                yield return waitFixed;
                Runner.Tick();
                onFixedStep?.Invoke();
            }
        }
    }
}

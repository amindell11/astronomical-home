using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One seed's live composition: the driver that runs its episodes, the pair the probes read, and the per-episode opponent install (the draw that fingerprints the episode's JSONL row).</summary>
    internal interface ISessionComposition : System.IDisposable
    {
        EpisodeLoopDriver Driver { get; }
        EpisodePair Pair { get; }
        OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter);
    }
}

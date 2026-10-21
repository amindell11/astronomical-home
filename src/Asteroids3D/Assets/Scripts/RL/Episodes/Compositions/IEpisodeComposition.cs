using RL.Episodes;

namespace RL.Episodes.Compositions
{
    internal interface IEpisodeComposition : System.IDisposable
    {
        EpisodeLoopDriver Driver { get; }
    }
}

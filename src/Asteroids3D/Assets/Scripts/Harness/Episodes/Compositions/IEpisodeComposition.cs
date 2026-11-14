using Harness.Episodes;

namespace Harness.Episodes.Compositions
{
    internal interface IEpisodeComposition : System.IDisposable
    {
        EpisodeLoopDriver Driver { get; }
    }
}

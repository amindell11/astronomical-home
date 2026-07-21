namespace Game.RLHarness
{
    internal interface IEpisodeComposition : System.IDisposable
    {
        EpisodeLoopDriver Driver { get; }
    }
}

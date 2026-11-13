using FMODUnity;

namespace Game.Audio
{
    public static class MixBuses
    {
        public const string World = "bus:/World";

        public static void SetWorldPaused(bool paused) => RuntimeManager.GetBus(World).setPaused(paused);
    }
}

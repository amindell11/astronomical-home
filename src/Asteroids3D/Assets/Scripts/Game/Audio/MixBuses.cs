using FMODUnity;

namespace Game.Audio
{
    public static class MixBuses
    {
        public const string Master = "bus:/";
        public const string Music = "bus:/Music";
        public const string World = "bus:/World";
        public const string Hud = "bus:/HUD";

        public static void SetWorldPaused(bool paused) => RuntimeManager.GetBus(World).setPaused(paused);

        public static void SetVolume(string bus, float volume) => RuntimeManager.GetBus(bus).setVolume(volume);
    }
}

using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Player volume per channel, persisted in PlayerPrefs and applied as Studio bus volume.
    /// API volume scales the Studio fader, so the authored mix stands.
    /// </summary>
    public static class VolumeSettings
    {
        public enum Channel { Master, Music, Effects }

        public static readonly Channel[] Channels = { Channel.Master, Channel.Music, Channel.Effects };

        // Keys are pinned so renaming the enum never drops a saved setting.
        private const string MasterKey = "volume.master";
        private const string MusicKey = "volume.music";
        private const string EffectsKey = "volume.effects";

        private static readonly string[] MasterBuses = { MixBuses.Master };
        private static readonly string[] MusicBuses = { MixBuses.Music };
        private static readonly string[] EffectsBuses = { MixBuses.World, MixBuses.Hud };

        public static string Label(Channel channel) => channel.ToString().ToUpperInvariant();

        public static float Get(Channel channel) => PlayerPrefs.GetFloat(Key(channel), 1f);

        public static void Set(Channel channel, float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(Key(channel), value);
            ApplyBuses(channel, value);
        }

        public static void Apply()
        {
            foreach (var channel in Channels)
                ApplyBuses(channel, Get(channel));
        }

        internal static string Key(Channel channel) => channel switch
        {
            Channel.Master => MasterKey,
            Channel.Music => MusicKey,
            Channel.Effects => EffectsKey
        };

        private static void ApplyBuses(Channel channel, float value)
        {
            foreach (var bus in Buses(channel))
                MixBuses.SetVolume(bus, value);
        }

        private static string[] Buses(Channel channel) => channel switch
        {
            Channel.Master => MasterBuses,
            Channel.Music => MusicBuses,
            Channel.Effects => EffectsBuses
        };
    }
}

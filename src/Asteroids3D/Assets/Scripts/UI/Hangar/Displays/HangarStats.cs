using System;
using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// The six ship stats the hull diagram shows, read off ship prefab assets where Awake never
    /// ran. A bar measures its ship against the best ship in the offer, with a floor so the
    /// weakest ship still shows a bar.
    /// </summary>
    public static class HangarStats
    {
        public const int Count = 6;
        public const float Floor = .12f;

        public static float Value(Ship ship, HangarStat stat) => stat switch
        {
            HangarStat.Hull => ship.maxHealth,
            HangarStat.Shield => ship.Shield ? ship.Shield.maxShield : 0f,
            HangarStat.Regen => ship.Shield ? ship.Shield.shieldRegenRate : 0f,
            HangarStat.Speed => ship.Engine.maxSpeed,
            HangarStat.Accel => ship.Engine.forwardForce / ship.mass,
            HangarStat.Turn => ship.Engine.maxYawRate,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };

        public static float Best(IReadOnlyList<Ship> ships, HangarStat stat)
        {
            var best = 0f;
            for (var i = 0; i < ships.Count; i++)
                best = Mathf.Max(best, Value(ships[i], stat));
            return best;
        }

        public static float Fill(float value, float best) =>
            best <= 0f ? Floor : Floor + (1f - Floor) * Mathf.Clamp01(value / best);

        public static string Label(HangarStat stat) => stat switch
        {
            HangarStat.Hull => "Hull",
            HangarStat.Shield => "Shield",
            HangarStat.Regen => "Regen",
            HangarStat.Speed => "Speed",
            HangarStat.Accel => "Accel",
            HangarStat.Turn => "Turn",
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };

        public static string Number(float value) => value.ToString("0.#");

        public static string Unit(HangarStat stat) => stat switch
        {
            HangarStat.Regen => "/s",
            HangarStat.Turn => "°/s",
            _ => ""
        };
    }
}

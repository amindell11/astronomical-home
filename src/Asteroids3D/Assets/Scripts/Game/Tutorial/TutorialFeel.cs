using System;
using Game.CloseUps;
using UnityEngine;

namespace Game.Tutorial
{
    [Serializable]
    public sealed class TutorialFeel
    {
        [Tooltip("The hunt intro's camera move onto a patrol. Its hold is how long the intro card reads.")]
        public CloseUpFeel introPan = new CloseUpFeel
        {
            slowScale = .25f,
            easeInSeconds = .8f,
            holdSeconds = 6f,
            easeOutSeconds = .8f,
            closeSize = 9f,
        };

        [Tooltip("The hunt intro's zoom out over the sector's stations, before the patrol. Its hold is how long the " +
                 "stations card reads; its close size is unused, since the zoom frames every station.")]
        public CloseUpFeel stationsZoom = new CloseUpFeel
        {
            slowScale = .25f,
            easeInSeconds = 1f,
            holdSeconds = 6f,
            easeOutSeconds = 1f,
        };

        [Tooltip("World units of room the stations zoom leaves around the player and the outermost stations.")]
        [Min(0f)] public float stationsMargin = 20f;

        [Tooltip("The part of the screen, in viewport fractions from the bottom left, the stations zoom fits the " +
                 "stations into: clear of the weapon dials below, the card and clock above, and the minimap.")]
        public Rect stationsBand = new(.04f, .24f, .74f, .20f);

        [Tooltip("Height on screen, as a fraction from the bottom, the patrol pan holds the patrol at, below the card.")]
        [Range(0f, 1f)] public float patrolScreenHeight = .36f;

        [Tooltip("Real seconds before a fire press can skip each hunt intro shot, so a player still firing from the weapon prompts can't skip it by accident.")]
        [Min(0f)] public float introSkipDelaySeconds = .6f;

        [Tooltip("Real seconds the waves card shows after the first alarm.")]
        [Min(0f)] public float wavesCardSeconds = 7f;
    }
}

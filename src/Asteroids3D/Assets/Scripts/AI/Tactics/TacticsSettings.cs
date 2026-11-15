using System;
using UnityEngine;

namespace AI.Tactics
{
    /// <summary>Every setting of the brain that picks tactics. Each pilot flies its own asset, its temper:
    /// Aggressor, Orbiter, Kiter, Lurker, the elite, its escorts.</summary>
    [CreateAssetMenu(fileName = "Temper", menuName = "AI/Tactics Settings")]
    public class TacticsSettings : ScriptableObject
    {
        [Header("Situation")]
        [Tooltip("Full width, in degrees, of the cone off the target's nose. A ship inside it and inside the " +
                 "target's primary gun range is exposed, and a side of the target inside it is never open.")]
        [Range(0f, 360f)] public float exposureCone = 60f;
        [Tooltip("Width, in units, of a ship's firing lane to its target. An ally whose centre sits inside it, " +
                 "between the ship and the target, is in the lane.")]
        [Min(0f)] public float laneWidth = 4f;

        [Header("Tactics")]
        [Tooltip("The tactic the ship flies when no rule overrides it.")]
        public Tactic home = Tactic.AttackRun;
        [Tooltip("Units beyond its gun hold range at which the ship flies its attack run (0 = at the hold range).")]
        [Min(0f)] public float attackRunMargin;
        [Tooltip("With its target gone, circle the wreck for a few laps before wandering. Off wanders from the start.")]
        public bool circlesWreck = true;
        [Tooltip("Seconds since the ship was last hit within which a gauge under its drawn point makes it hurt.")]
        [Min(0f)] public float hitWithin = 2f;
        [Tooltip("Shield %, below which a ship in the target's guns flanks or stands off instead of pressing. " +
                 "At 0 it always presses; at 100 it never flies into the guns, even with a full shield.")]
        [Range(0f, 100f)] public float nerveShield = 50f;
        [Tooltip("Units outside the target's primary gun range at which a ship standing off waits.")]
        [Min(0f)] public float standOffMargin = 5f;
        [Tooltip("Seconds a ship holds every tactic it picks, break off included, before it can pick again.")]
        [Min(0f)] public float minimumHold = 2f;

        [Header("When hurt")]
        [Tooltip("What a hurt ship does. Break off stops firing. Fall back keeps firing. Turn cautious stays out of the target's guns. Never ignores the wound.")]
        public WoundedResponse wounded = WoundedResponse.BreakOff;
        [Tooltip("Shield or hull, the gauge a ship reads to decide it is hurt.")]
        public WoundGauge woundedBy = WoundGauge.Shield;
        [Tooltip("Gauge %, below which a ship hit within the last 'hit within' seconds is hurt. Each ship draws its own point in this range.")]
        public Spread woundedBelow = new(25f, 25f);
        [Tooltip("What ends being hurt: the shield coming back, or the ship's drawn number of seconds.")]
        public WoundedEnds woundedEnds = WoundedEnds.WhenShieldIsBack;
        [Tooltip("Shield %, at or above which a ship is no longer hurt, when the shield coming back ends it. Each ship draws its own level in this range.")]
        public Spread shieldBack = new(80f, 80f);
        [Tooltip("Seconds a ship stays hurt, when seconds end it. Each ship draws its own length in this range.")]
        public Spread woundedSeconds = new(5f, 5f);
        [Tooltip("After its drawn number of retreats the ship is never hurt again. Off means no limit.")]
        public bool commits;
        [Tooltip("Fewest retreats a ship can draw.")]
        [Min(1)] public int retreatsMin = 1;
        [Tooltip("Most retreats a ship can draw.")]
        [Min(1)] public int retreatsMax = 1;
        [Tooltip("Units inside its own primary gun range at which a ship falling back holds.")]
        [Min(0f)] public float fallBackMargin = 1f;

        private void OnValidate()
        {
            if (home == Tactic.BreakOff)
                throw new InvalidOperationException($"{name}: a ship cannot make breaking off its home tactic");
            if (woundedBelow.min > woundedBelow.max)
                throw new InvalidOperationException($"{name}: wounded below min is above its max");
            if (shieldBack.min > shieldBack.max)
                throw new InvalidOperationException($"{name}: shield back min is above its max");
            if (woundedSeconds.min > woundedSeconds.max)
                throw new InvalidOperationException($"{name}: wounded seconds min is above its max");
            if (retreatsMin > retreatsMax)
                throw new InvalidOperationException($"{name}: retreats min is above its max");
        }
    }
}

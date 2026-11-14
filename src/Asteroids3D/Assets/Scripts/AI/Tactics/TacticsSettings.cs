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
        [Tooltip("Shield %, below which a ship hit within the last 'hit within' seconds breaks off.")]
        [Range(0f, 100f)] public float breakOffShield = 25f;
        [Tooltip("Shield %, at or above which a ship breaking off comes back to pick again.")]
        [Range(0f, 100f)] public float resumeShield = 80f;
        [Tooltip("Seconds since the ship was last hit within which a low shield makes it break off.")]
        [Min(0f)] public float hitWithin = 2f;
        [Tooltip("Shield %, below which a ship in the target's guns flanks or stands off instead of pressing. " +
                 "At 0 it always presses; at 100 it never flies into the guns, even with a full shield.")]
        [Range(0f, 100f)] public float nerveShield = 50f;
        [Tooltip("Units outside the target's primary gun range at which a ship standing off waits.")]
        [Min(0f)] public float standOffMargin = 5f;
        [Tooltip("Seconds a ship holds every tactic it picks, break off included, before it can pick again.")]
        [Min(0f)] public float minimumHold = 2f;

        private void OnValidate()
        {
            if (home != Tactic.BreakOff) return;
            throw new InvalidOperationException($"{name}: a ship cannot make breaking off its home tactic");
        }
    }
}

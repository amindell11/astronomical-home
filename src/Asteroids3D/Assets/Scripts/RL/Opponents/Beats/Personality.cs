using UnityEngine;

namespace RL.Opponents.Beats
{
    /// <summary>
    /// One kind of enemy pilot's weight set for picking beats: how much it wants each beat, how hard a
    /// crowded target puts it off, how long it commits to a beat, and the shape of the beats it flies.
    /// A pilot prefab references one, and each enemy flies its own draw from it, different every run
    /// (<see cref="variation"/>). A faction role is another asset, not another code path.
    /// </summary>
    [CreateAssetMenu(menuName = "AI/Pilot Personality", fileName = "Personality")]
    public sealed class Personality : ScriptableObject
    {
        [Header("Desire")]
        [Tooltip("How much this pilot wants an attack run: closing to its weapon's range with both triggers live.")]
        [Min(0f)] public float attack = 1f;
        [Tooltip("How much it wants to flank to its target's blind side. Up to double while the target's nose points at it.")]
        [Min(0f)] public float flank = 0.5f;
        [Tooltip("How much it wants to hold at range, circling its target and taking occasional long shots.")]
        [Min(0f)] public float hold = 0.6f;
        [Tooltip("How much a low shield makes it break off so the shield can regenerate. Halved when it hasn't been hit lately.")]
        [Min(0f)] public float breakOff = 1.5f;

        [Header("Crowding")]
        [Tooltip("Score an attack run or a flank loses for each ally past the tolerance already attacking the same target.")]
        [Min(0f)] public float crowding = 0.8f;
        [Tooltip("Allies already attacking the same target that this pilot ignores.")]
        [Min(0)] public int tolerance = 1;

        [Header("Shield")]
        [Tooltip("Shield fraction below which breaking off starts to appeal, rising to full appeal at an empty shield.")]
        [Range(0f, 1f)] public float breakOffShield = 0.35f;
        [Tooltip("A break-off ends early once the shield is back to this fraction.")]
        [Range(0f, 1f)] public float resumeShield = 0.8f;
        [Tooltip("Seconds a hit still counts as recent.")]
        [Min(0f)] public float hitMemorySeconds = 2f;

        [Header("Commitment (s)")]
        [Tooltip("Before this, no beat ends early; a beat shorter than this still ends at its length.")]
        [Min(0f)] public float minBeatSeconds = 1.5f;
        [Tooltip("An attack run's length is drawn between x and y.")]
        public Vector2 attackSeconds = new(4f, 7f);
        [Tooltip("A flank's length is drawn between x and y; it ends early on reaching the blind side.")]
        public Vector2 flankSeconds = new(3f, 5f);
        [Tooltip("A hold's length is drawn between x and y.")]
        public Vector2 holdSeconds = new(3f, 6f);
        [Tooltip("A break-off's length is drawn between x and y.")]
        public Vector2 breakOffSeconds = new(3f, 6f);

        [Header("Beat shape")]
        [Tooltip("A hold circles at this multiple of the attack run's range, pulled inside the primary's fire distance.")]
        [Min(1f)] public float holdRangeScale = 1.6f;
        [Tooltip("Distance (u) behind the target that a flank aims for.")]
        [Min(0f)] public float flankDistance = 14f;
        [Tooltip("Seconds between the long shots a hold takes.")]
        [Min(0.1f)] public float longShotInterval = 2f;
        [Tooltip("Seconds each long shot keeps the primary trigger live.")]
        [Min(0f)] public float longShotSeconds = 0.6f;

        [Header("Variation")]
        [Tooltip("Each enemy's desires and crowding are drawn within this fraction either side of the values above, fresh each run.")]
        [Range(0f, 1f)] public float variation = 0.25f;
    }
}

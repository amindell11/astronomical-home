using UnityEngine;

namespace Game.Companion
{
    /// <summary>
    /// The companion's named knobs: the nag budget and how long a line holds for its length and fades. The
    /// game reads the asset its host serializes; the preview host edits that same asset live and saves it
    /// explicitly.
    /// </summary>
    [CreateAssetMenu(fileName = "CompanionFeel", menuName = "Game/Companion Feel")]
    public sealed class CompanionFeelProfile : ScriptableObject
    {
        [Tooltip("Least seconds between two spoken lines. A must-say line slot skips the wait.")]
        [SerializeField, Min(0f)] private float minLineIntervalSeconds = 25f;

        [Tooltip("Seconds a line may wait to be shown, such as behind a hidden HUD, before it is dropped.")]
        [SerializeField, Min(0f)] private float staleSeconds = 3f;

        [Tooltip("Least seconds a line stays fully shown. A longer line holds until it can be read.")]
        [SerializeField, Min(0f)] private float holdSeconds = 4f;

        [Tooltip("Characters a player reads per second mid-fight. Sets how long a long line holds.")]
        [SerializeField, Min(1f)] private float readingCharactersPerSecond = 12f;

        [Tooltip("Seconds a line takes to fade out after its hold.")]
        [SerializeField, Min(0f)] private float fadeSeconds = .4f;

        [Tooltip("Chance that an opening line with a possible reply is followed by one.")]
        [SerializeField, Range(0f, 1f)] private float replyChance = .6f;

        public float MinLineIntervalSeconds => minLineIntervalSeconds;
        public float StaleSeconds => staleSeconds;
        public float HoldFor(string line) => Mathf.Max(holdSeconds, line.Length / readingCharactersPerSecond);
        public float FadeSeconds => fadeSeconds;
        public float ReplyChance => replyChance;
    }
}

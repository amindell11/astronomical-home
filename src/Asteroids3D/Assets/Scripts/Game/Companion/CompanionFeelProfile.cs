using UnityEngine;

namespace Game.Companion
{
    /// <summary>
    /// The companion's named knobs: the nag budget and how long a line holds and fades. The game reads the
    /// asset its host serializes; the preview host edits that same asset live and saves it explicitly.
    /// </summary>
    [CreateAssetMenu(fileName = "CompanionFeel", menuName = "Game/Companion Feel")]
    public sealed class CompanionFeelProfile : ScriptableObject
    {
        [Tooltip("Least seconds between two spoken lines. A must-say line slot skips the wait.")]
        [SerializeField, Min(0f)] private float minLineIntervalSeconds = 25f;

        [Tooltip("Seconds a line may wait to be shown, such as behind a hidden HUD, before it is dropped.")]
        [SerializeField, Min(0f)] private float staleSeconds = 3f;

        [Tooltip("Seconds a line stays fully shown.")]
        [SerializeField, Min(0f)] private float holdSeconds = 4f;

        [Tooltip("Seconds a line takes to fade out after its hold.")]
        [SerializeField, Min(0f)] private float fadeSeconds = .4f;

        public float MinLineIntervalSeconds => minLineIntervalSeconds;
        public float StaleSeconds => staleSeconds;
        public float HoldSeconds => holdSeconds;
        public float FadeSeconds => fadeSeconds;
    }
}

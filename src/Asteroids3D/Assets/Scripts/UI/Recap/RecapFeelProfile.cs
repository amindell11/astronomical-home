using UnityEngine;

namespace UI.Recap
{
    /// <summary>
    /// The named knobs of the recap sequence: how long each fade and gap takes. The game reads the
    /// asset its host serializes; the preview host edits that same asset live and saves it explicitly.
    /// </summary>
    [CreateAssetMenu(fileName = "RecapFeel", menuName = "Game/Recap Feel")]
    public sealed class RecapFeelProfile : ScriptableObject
    {
        [Tooltip("Seconds the HUD takes to fade out when a run ends.")]
        [SerializeField, Min(0f)] private float hudFadeSeconds = .3f;

        [Tooltip("Seconds the outcome banner takes to fade in.")]
        [SerializeField, Min(0f)] private float bannerInSeconds = .8f;

        [Tooltip("Seconds the outcome banner takes to fade out after the player's press.")]
        [SerializeField, Min(0f)] private float bannerOutSeconds = .7f;

        [Tooltip("Seconds of empty screen between the banner and the breakdown.")]
        [SerializeField, Min(0f)] private float gapSeconds = .3f;

        [Tooltip("Seconds the breakdown card takes to fade in.")]
        [SerializeField, Min(0f)] private float breakdownInSeconds = .8f;

        public float HudFadeSeconds => hudFadeSeconds;
        public float BannerInSeconds => bannerInSeconds;
        public float BannerOutSeconds => bannerOutSeconds;
        public float GapSeconds => gapSeconds;
        public float BreakdownInSeconds => breakdownInSeconds;
    }
}

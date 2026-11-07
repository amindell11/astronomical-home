#if UNITY_EDITOR
using AI.Gunnery;
using UnityEngine;

namespace Tests.Common
{
    public static class TestAimSettings
    {
        private static AimSettings perfect;

        /// <summary>Every distortion off, so the Gunner sees the true target; shared and never mutated.</summary>
        public static AimSettings Perfect => perfect ? perfect : perfect = CreatePerfect();

        private static AimSettings CreatePerfect()
        {
            var settings = ScriptableObject.CreateInstance<AimSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.reactionDelay = 0f;
            settings.wanderSize = 0f;
            settings.startingError = 0f;
            return settings;
        }
    }
}
#endif

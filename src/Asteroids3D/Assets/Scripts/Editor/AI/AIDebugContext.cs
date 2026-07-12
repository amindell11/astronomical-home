using UnityEditor;

namespace AI.Debug
{
    /// <summary>Editor-side access to the singleton <see cref="AIDebugSettings"/> asset. Debug
    /// tooling config is never serialized on gameplay prefabs; consumers resolve it here.</summary>
    public static class AIDebugContext
    {
        private static AIDebugSettings cached;

        public static AIDebugSettings Settings
        {
            get
            {
                if (cached) return cached;
                var guids = AssetDatabase.FindAssets("t:AIDebugSettings");
                if (guids.Length > 0)
                    cached = AssetDatabase.LoadAssetAtPath<AIDebugSettings>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                return cached;
            }
        }

        public static bool ShouldDraw(AIDebugChannel channel, bool isSelected)
        {
            var settings = Settings;
            return settings && settings.ShouldDraw(isSelected) && settings.IsActive(channel);
        }
    }
}

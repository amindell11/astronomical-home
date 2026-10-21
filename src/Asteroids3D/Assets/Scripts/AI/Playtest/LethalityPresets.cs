using UnityEngine;

namespace AI.Playtest
{
    public enum LethalityPreset
    {
        Main = 1,
        Servo = 2,
        EarnedAccuracy = 3,
        Dial025 = 4,
        Dial075EarnedAccuracy = 5,
    }

    /// <summary>Throwaway playtest switch (branch playtest/lethality-presets, never merged): the enemy-AI aim preset every AI ship reads live. Constants are the research rig's calibration values (research/ai-gun-strategies, crowd r3).</summary>
    public static class LethalityPresets
    {
        public static LethalityPreset Active = LethalityPreset.Servo;

        public const float DialReactionMax = 0.5f;
        public const float DialNoiseMaxDeg = 15f;
        public const float DialNoiseTau = 0.5f;
        public const float DialAccelMin = 0.05f;

        public const float EaE0Deg = 20f;
        public const float EaT = 6f;
        public const float EaConeDeg = 30f;
        public const float EaRange = 25f;
        public const float EaJinkAccel = 5f;
        public const float EaNoiseTau = 0.5f;
        public const float EaAccelSmooth = 0.1f;

        public static bool ServoOn => Active != LethalityPreset.Main;

        public static float Skill => Active switch
        {
            LethalityPreset.Dial025 => 0.25f,
            LethalityPreset.Dial075EarnedAccuracy => 0.75f,
            _ => -1f,
        };

        public static bool EarnedAccuracyOn =>
            Active == LethalityPreset.EarnedAccuracy || Active == LethalityPreset.Dial075EarnedAccuracy;

        public static float AccelFraction(float servoFraction) =>
            Skill < 0f ? servoFraction : Mathf.Lerp(DialAccelMin, servoFraction, Skill);

        public static string Label(LethalityPreset preset) => preset switch
        {
            LethalityPreset.Main => "Main's AI (servo off)",
            LethalityPreset.Servo => "Full servo (#1062)",
            LethalityPreset.EarnedAccuracy => "Earned accuracy 20 deg / 6 s",
            LethalityPreset.Dial025 => "Skill dial 0.25",
            LethalityPreset.Dial075EarnedAccuracy => "Dial 0.75 + earned accuracy 20 deg / 6 s",
            _ => preset.ToString(),
        };
    }
}

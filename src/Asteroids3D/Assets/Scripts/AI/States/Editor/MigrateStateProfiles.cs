#if UNITY_EDITOR
using System.Collections.Generic;
using AI.States;
using UnityEditor;
using UnityEngine;

namespace AI.States.Editor
{
    /// <summary>
    /// One-shot migration script to convert old flat StateProfile fields
    /// to the new [SerializeReference] UtilityFactor and GoalStrategy types.
    /// Run via menu: AI/Migrate State Profiles. Delete this file after migration.
    /// </summary>
    public static class MigrateStateProfiles
    {
        [MenuItem("AI/Migrate State Profiles")]
        public static void Migrate()
        {
            var guids = AssetDatabase.FindAssets("t:AI.States.StateProfile");
            var count = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<StateProfile>(path);
                if (profile == null) continue;

                if (profile.goal != null && profile.utilityFactors != null
                    && profile.utilityFactors.Count > 0 && profile.utilityFactors[0] != null)
                {
                    UnityEngine.Debug.Log($"[Migration] Skipping {profile.name} — already migrated.");
                    continue;
                }

                if (!MigrateProfile(profile))
                {
                    UnityEngine.Debug.LogWarning($"[Migration] Unknown profile '{profile.name}' — skipping.");
                    continue;
                }

                EditorUtility.SetDirty(profile);
                count++;
                UnityEngine.Debug.Log($"[Migration] Migrated {profile.name} at {path}");
            }

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[Migration] Done. Migrated {count} profile(s).");
        }

        private static bool MigrateProfile(StateProfile p)
        {
            switch (p.name)
            {
                case "Attack":
                    p.goal = new TrackEnemyGoal { desiredRange = 23f, rangeTolerance = 17f };
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        MakeCurve(ContinuousInput.Health, 0.35f, 1f),      // s=0.35, p=1 → (0.65, 1.35)
                        MakeCurve(ContinuousInput.Shield, 0.3f, 1f),       // s=0.3, p=1 → (0.7, 1.3)
                        MakeCurve(ContinuousInput.EnemyDurability, 0.15f, -1f), // s=0.15, p=-1 → (1.15, 0.85)
                        new RangeBandFactor { optimalMin = 6f, optimalMax = 40f },
                        MakeBinary(BinarySignal.LOS, 0.4f, 1f),            // s=0.4, p=1 → (0.6, 1.4)
                        MakeCurve(ContinuousInput.Outnumbered, 0.25f, -1f), // s=0.25, p=-1 → (1.25, 0.75)
                        MakeCurve(ContinuousInput.Health, 0.1f, -1f),       // s=0.1, p=-1 → (1.1, 0.9)
                    };
                    // facingWidth=0, exposureWidth=0 → both become 1 (use base)
                    SetWidthMultipliers(p, 1f, 1f);
                    return true;

                case "AttackAggressive":
                    p.goal = new TrackEnemyGoal { desiredRange = 15.5f, rangeTolerance = 9.5f };
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        MakeCurve(ContinuousInput.Health, 0.45f, 1f),      // (0.55, 1.45)
                        MakeCurve(ContinuousInput.Shield, 0.35f, 1f),      // (0.65, 1.35)
                        MakeCurve(ContinuousInput.EnemyDurability, 0.15f, -1f), // (1.15, 0.85)
                        new RangeBandFactor { optimalMin = 6f, optimalMax = 25f },
                        MakeBinary(BinarySignal.LOS, 0.4f, 1f),            // (0.6, 1.4)
                        MakeCurve(ContinuousInput.EnemyFacing, 0.35f, -1f), // (1.35, 0.65)
                        MakeCurve(ContinuousInput.SelfAngle, 0.35f, -1f),   // (1.35, 0.65)
                    };
                    // facingWidth=0.3 → 0.3/0.5 = 0.6, exposureWidth=0 → 1
                    SetWidthMultipliers(p, 0.6f, 1f);
                    return true;

                case "AttackEvasive":
                    p.goal = new TrackEnemyGoal { desiredRange = 28.5f, rangeTolerance = 16.5f };
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        MakeCurve(ContinuousInput.Health, 0.25f, -1f),      // (1.25, 0.75)
                        MakeCurve(ContinuousInput.Shield, 0.15f, -1f),      // (1.15, 0.85)
                        new RangeBandFactor { optimalMin = 12f, optimalMax = 45f },
                        MakeBinary(BinarySignal.LOS, 0.35f, 1f),            // (0.65, 1.35)
                        MakeCurve(ContinuousInput.EnemyFacing, 0.45f, 1f),   // (0.55, 1.45)
                        MakeCurve(ContinuousInput.ClosingRate, 0.3f, 1f),    // (0.7, 1.3)
                    };
                    // facingWidth=0 → 1, exposureWidth=0.8 → 0.8/0.5 = 1.6
                    SetWidthMultipliers(p, 1f, 1.6f);
                    return true;

                case "Evade":
                    p.goal = new FleeEnemyGoal();
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        MakeCurve(ContinuousInput.Health, 0.3f, -1f),       // (1.3, 0.7)
                        MakeCurve(ContinuousInput.Shield, 0.2f, -1f),       // (1.2, 0.8)
                        MakeCurve(ContinuousInput.Outnumbered, 0.25f, 1f),   // (0.75, 1.25)
                        MakeBinary(BinarySignal.LOS, 0.25f, 1f),            // (0.75, 1.25)
                        MakeCurve(ContinuousInput.ClosingRate, 0.2f, 1f),    // (0.8, 1.2)
                        MakeCurve(ContinuousInput.EnemyFacing, 0.15f, 1f),   // (0.85, 1.15)
                        MakeCurve(ContinuousInput.SelfAngle, 0.15f, 1f),     // (0.85, 1.15)
                        new BinaryFactor { signal = BinarySignal.IncomingMissile, whenTrue = 1.2f, whenFalse = 1f },
                        new ProximityThreatFactor { dangerDistance = 7f, boost = 1.3f },
                        new FightingRetreatFactor { healthThreshold = 0.5f, shieldThreshold = 0.5f, boost = 1.25f },
                    };
                    // facingWidth=0, exposureWidth=0 → both become 1
                    SetWidthMultipliers(p, 1f, 1f);
                    return true;

                case "Patrol":
                    p.goal = new RandomWaypointGoal
                    {
                        patrolRadius = 50f,
                        minDistanceFactor = 0.3f,
                        arriveRadius = 3f,
                        stuckTimeout = 3f,
                        stuckProgressThreshold = 1f,
                    };
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        new BinaryFactor { signal = BinarySignal.NoCombat, whenTrue = 2.0f, whenFalse = 0.01f },
                    };
                    SetWidthMultipliers(p, 1f, 1f);
                    return true;

                case "Pursuit":
                    p.goal = new TrackEnemyGoal { desiredRange = 0f, rangeTolerance = 0f };
                    p.utilityFactors = new List<UtilityFactor>
                    {
                        MakeCurve(ContinuousInput.Health, 0.3f, 1f),        // (0.7, 1.3)
                        MakeCurve(ContinuousInput.Shield, 0.25f, 1f),       // (0.75, 1.25)
                        new DistanceFactor
                        {
                            engageDistance = 50f,
                            response = MakeAnimCurve(0.55f, 1.45f),          // s=0.45, p=1 → (0.55, 1.45)
                        },
                        MakeCurve(ContinuousInput.Outnumbered, 0.25f, -1f),  // (1.25, 0.75)
                    };
                    SetWidthMultipliers(p, 1f, 1f);
                    return true;

                default:
                    return false;
            }
        }

        private static void SetWidthMultipliers(StateProfile p, float facing, float exposure)
        {
            var m = p.weightMultipliers;
            m.facingWidth = facing;
            m.exposureWidth = exposure;
            p.weightMultipliers = m;
        }

        /// <summary>Compute atLow/atHigh from old (strength, preference) and create a CurveFactor.</summary>
        private static CurveFactor MakeCurve(ContinuousInput input, float strength, float preference)
        {
            var atLow = Mathf.Clamp(1f - strength * preference, 0f, 2f);
            var atHigh = Mathf.Clamp(1f + strength * preference, 0f, 2f);
            return new CurveFactor { input = input, response = MakeAnimCurve(atLow, atHigh) };
        }

        /// <summary>Compute whenTrue/whenFalse from old (strength, preference) and create a BinaryFactor.</summary>
        private static BinaryFactor MakeBinary(BinarySignal signal, float strength, float preference)
        {
            var atLow = Mathf.Clamp(1f - strength * preference, 0f, 2f);
            var atHigh = Mathf.Clamp(1f + strength * preference, 0f, 2f);
            return new BinaryFactor { signal = signal, whenTrue = atHigh, whenFalse = atLow };
        }

        private static AnimationCurve MakeAnimCurve(float atLow, float atHigh)
        {
            return new AnimationCurve(
                new Keyframe(0f, atLow, 0f, 0f),
                new Keyframe(1f, atHigh, 0f, 0f));
        }
    }
}
#endif

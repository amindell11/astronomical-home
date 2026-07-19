using System;
using AI;
using AI.Context;
using AI.States;
using Movement;
using Movement.MPC;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.RLHarness
{
    /// <summary>The gate driver: a constant commanded velocity across the field, free-yaw (no target, so the solver yaws to exploit forward thrust) — measures pure velocity-mode obstacle competence.</summary>
    public class VelocityTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "velocity-ref";

        private Vector2 velocityReference;

        public void Configure(Vector2 crossingDir, float speed) =>
            velocityReference = crossingDir.normalized * speed;

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (ctx?.Self == null) return NavigationIntent.None;
            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = velocityReference,
            };
        }
    }

    /// <summary>The legacy comparator: the authored stack's TRAVERSAL identity — Waypoint mode (the wPos gradient patrol flight rides) with the Patrol profile's own weight overrides, aimed far past the crossing exit so arrival deceleration never enters the measured segment. Answers "how fast can the authored stack cross the field"; the MaintainRange closing regimes crawl regardless of overrides (measured on #173) and measure pursuit, not traversal.</summary>
    public class LegacyNavTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "legacy-waypoint";
        public const string PatrolProfilePath = "Assets/Settings/AI/StateProfiles/Patrol.asset";

        private static StateProfile patrolProfile;
        public static StateProfile PatrolProfile
        {
            get
            {
#if UNITY_EDITOR
                if (!patrolProfile)
                {
                    patrolProfile = AssetDatabase.LoadAssetAtPath<StateProfile>(PatrolProfilePath);
                    if (!patrolProfile)
                        throw new InvalidOperationException($"Failed to load {PatrolProfilePath} — check probe asset paths.");
                }
                return patrolProfile;
#else
                throw new NotSupportedException("The legacy comparator loads the Patrol profile via AssetDatabase (editor only).");
#endif
            }
        }

        private Vector2 waypoint;

        public void Configure(Vector2 waypoint) => this.waypoint = waypoint;

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (ctx?.Self == null) return NavigationIntent.None;
            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.Waypoint,
                goalPosition = waypoint,
                weightOverrides = PatrolProfile.weightOverrides,
            };
        }
    }
}

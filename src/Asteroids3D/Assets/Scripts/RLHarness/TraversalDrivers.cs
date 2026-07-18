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

    /// <summary>The legacy comparator: the same crossing through the authored pursuit regime — the Pursuit state profile's own goal params, weight overrides (momentum-dominant, facing-free), and tactical flag, aimed at a marker past the far edge (nav/terminal-field bake still keys on MaintainRange-with-target). Reading the asset keeps the comparator honest: bare base-weight MaintainRange is a configuration no authored state ever runs.</summary>
    public class LegacyNavTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "legacy-nav";
        public const string PursuitProfilePath = "Assets/Settings/AI/StateProfiles/Pursuit.asset";

        private static StateProfile pursuitProfile;
        private static TrackEnemyGoal PursuitGoal =>
            (PursuitProfile.goal as TrackEnemyGoal)
            ?? throw new InvalidOperationException("Pursuit profile's goal must be a TrackEnemyGoal");

        public static StateProfile PursuitProfile
        {
            get
            {
#if UNITY_EDITOR
                if (!pursuitProfile)
                {
                    pursuitProfile = AssetDatabase.LoadAssetAtPath<StateProfile>(PursuitProfilePath);
                    if (!pursuitProfile)
                        throw new InvalidOperationException($"Failed to load {PursuitProfilePath} — check probe asset paths.");
                }
                return pursuitProfile;
#else
                throw new NotSupportedException("The legacy comparator loads the Pursuit profile via AssetDatabase (editor only).");
#endif
            }
        }

        /// <summary>Place the goal marker this far past the crossing exit so the whole range-hold band lies beyond the finish line — the ship cannot settle short of it.</summary>
        public static float GoalStandoff => PursuitGoal.desiredRange + PursuitGoal.rangeTolerance;

        private DummyTarget destination;
        private float projectileSpeed;

        public void Configure(DummyTarget destination, float projectileSpeed)
        {
            this.destination = destination;
            this.projectileSpeed = projectileSpeed;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (destination == null || ctx?.Self == null) return NavigationIntent.None;
            var profile = PursuitProfile;
            var goal = PursuitGoal;
            var destPlane = destination.PlanePosition;
            return new NavigationIntent
            {
                isValid = true,
                goalMode = goal.GoalMode,
                goalPosition = destPlane,
                desiredRange = goal.desiredRange,
                rangeTolerance = goal.rangeTolerance,
                weightOverrides = profile.weightOverrides,
                enableFiring = profile.enableFiring,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = new Kinematics(destPlane, Vector2.zero, 0f, 0f, 0f),
                    dynamics = ctx.Self.Dynamics,
                    source = destination.transform,
                },
                applyTacticalCosts = profile.enableTacticalCosts,
                projectileSpeed = profile.enableTacticalCosts ? projectileSpeed : 0f,
            };
        }
    }
}

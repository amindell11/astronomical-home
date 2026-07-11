using AI.Context;
using AI.Utility;
using Movement;
using Movement.MPC;
using Ships.Command;
using UnityEngine;

namespace AI.States
{
    /// <summary>
    /// Unified state implementation driven by a StateProfile asset.
    /// Replaces all concrete state classes (Attack, Patrol, Evade, etc.)
    /// with a single data-driven class.
    /// </summary>
    public partial class AIState
    {
        protected readonly Navigator navigator;
        protected readonly Gunner gunner;
        private readonly GoalRunner goalRunner;

        public StateProfile Profile { get; }

        public string ProfileName => Profile.name;

        /// <summary>
        /// The NavigationIntent produced by the most recent Tick.
        /// Read by AICommander to apply to Navigator and Gunner.
        /// </summary>
        public NavigationIntent LastIntent { get; private set; }

        public AIState(StateProfile profile, Navigator navigator, Gunner gunner, SeedScope goalScope)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            Profile = profile;
            goalRunner = GoalRunner.Create(profile.goal, navigator, goalScope);
        }

        public void Enter(AIContext ctx)
        {
            // Goal mode, weights, firing, and enemy state are all (re)applied every Tick
            // through Navigator.ApplyIntent / Gunner.ApplyIntent. Enter only does one-shot
            // setup Tick can't redo.
            goalRunner.Enter(ctx);
        }

        /// <summary>
        /// Produces this state's action for the tick and returns it. Actuation is the
        /// commander's job: it applies the chosen intent to the Navigator and Gunner.
        /// See <see cref="AI.IIntentChooser"/>.
        /// </summary>
        public NavigationIntent Tick(AIContext ctx, float deltaTime)
        {
            LastIntent = ProduceIntent(ctx, deltaTime);
            return LastIntent;
        }

        public void Exit()
        {
            // Navigator reset is the commander's job: on a transition the brain returns
            // NavigationIntent.None, which the commander applies to reset the navigator.
            goalRunner.Reset();
            LastIntent = NavigationIntent.None;
        }

        private NavigationIntent ProduceIntent(AIContext ctx, float deltaTime)
        {
            var goal = Profile.goal;
            var intent = new NavigationIntent
            {
                isValid = true,
                goalMode = goal?.GoalMode ?? GoalMode.Waypoint,
                enableFiring = Profile.enableFiring,
                weightOverrides = Profile.weightOverrides,
            };

            // Goal owns the motion fields; the state layers on its tactical modulation.
            goalRunner.Fill(ctx, deltaTime, ref intent);
            ApplyTacticalModulation(ctx, ref intent);

            return intent;
        }

        /// <summary>Attaches the enemy target and the profile's consumption flags. The target
        /// is a single ship-agnostic snapshot; the navigator and gunner pull what they need
        /// (MPC tactical costs / firing solution). Only meaningful when an enemy is present.</summary>
        private void ApplyTacticalModulation(AIContext ctx, ref NavigationIntent intent)
        {
            if (!ctx.Combat.TryGetTarget(out var target)) return;

            intent.hasTarget = true;
            intent.target = target;
            intent.applyTacticalCosts = Profile.enableTacticalCosts;
            if (Profile.enableTacticalCosts)
                intent.projectileSpeed = gunner ? gunner.PrimaryProjectileSpeed : 0f;
        }
    }
}

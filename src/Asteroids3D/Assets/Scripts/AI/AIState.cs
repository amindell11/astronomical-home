using System.Linq;
using AI.Context;
using AI.Utility;
using Movement;
using Movement.MPC;
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

        public UtilityBuilder LastBuilder { get; private set; }

        /// <summary>
        /// The NavigationIntent produced by the most recent Tick.
        /// Read by AICommander to apply to Navigator and Gunner.
        /// </summary>
        public NavigationIntent LastIntent { get; private set; }

        public AIState(StateProfile profile, Navigator navigator, Gunner gunner)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            Profile = profile;
            goalRunner = GoalRunner.Create(profile.goal, navigator);
        }

        public bool IsAvailable(AIContext ctx) => Profile.IsAvailable(ctx);

        public void Enter(AIContext ctx)
        {
            // Goal mode, weights, firing, and enemy state are all (re)applied every Tick
            // through Navigator.ApplyIntent / Gunner.ApplyIntent. Enter only does one-shot
            // setup Tick can't redo.
            goalRunner.Enter(ctx);
        }

        public void Tick(AIContext ctx, float deltaTime)
        {
            LastIntent = ProduceIntent(ctx, deltaTime);
            navigator.ApplyIntent(LastIntent);
            if (gunner) gunner.ApplyIntent(LastIntent);
        }

        public void Exit()
        {
            navigator.ResetNavigation();
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

        /// <summary>State-level enrichment gated by profile flags: tactical MPC costs and
        /// gunner lead. Only meaningful when an enemy is present.</summary>
        private void ApplyTacticalModulation(AIContext ctx, ref NavigationIntent intent)
        {
            if (!ctx.Combat.HasEnemy) return;

            if (Profile.enableTacticalCosts)
                SetEnemyTactical(ctx, ref intent);

            if (Profile.enableFiring)
                MarkGunnerEnemy(ctx, ref intent);
        }

        /// <summary>Declares the enemy for the gunner to engage. The gunner resolves the
        /// firing solution (intercept lead) itself in <see cref="Gunner.ApplyIntent"/>.</summary>
        private static void MarkGunnerEnemy(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            intent.hasGunnerEnemy = true;
            intent.gunnerEnemyPos = combat.EnemyPos;
            intent.gunnerEnemyVel = combat.EnemyVel;
        }

        private void SetEnemyTactical(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            var enemyFwd = combat.EnemyForward;
            intent.hasEnemy = true;
            intent.enemyYawDeg = Mathf.Atan2(-enemyFwd.x, enemyFwd.y) * Mathf.Rad2Deg;
            intent.enemyYawRateDeg = combat.EnemyYawRate;
            intent.projectileSpeed = gunner.PrimaryProjectileSpeed;
            intent.enemyDynamics = combat.EnemyDynamics;
        }
        
        // ── Utility Scoring ──

        public float ComputeUtility(AIContext ctx)
        {
            var builder = NewBuilder();
            if (Profile.utilityFactors == null) return builder.Build();
            foreach (var factor in Profile.utilityFactors.Where(factor => factor != null)) {
                builder.Factor(factor.Name, factor.Evaluate(ctx.Assessment), factor.weight);
            }
            return builder.Build();
        }

        private UtilityBuilder NewBuilder()
        {
            LastBuilder = new UtilityBuilder();
            return LastBuilder;
        }
    }
}

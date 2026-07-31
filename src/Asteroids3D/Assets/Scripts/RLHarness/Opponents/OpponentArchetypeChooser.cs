using AI;
using AI.Context;
using AI.States;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Shared skeleton for the scripted opponent archetypes: live-target validity, the 5 Hz recompute cache, and the border tangent-steer post-step. Arena bounds enter once through Configure as plain floats.</summary>
    public abstract class OpponentArchetypeChooser : IIntentChooser
    {
        protected const int RecomputeIntervalTicks = 10;

        protected Ship target;
        protected float speedFraction;
        private Vector2 arenaCenter;
        private float borderRadius;

        private int tickCounter;
        private NavigationIntent cachedIntent = NavigationIntent.None;

        protected void Bind(Ship target, float speedFraction, Vector2 arenaCenter, float borderRadius)
        {
            this.target = target;
            this.speedFraction = speedFraction;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            Reset();
        }

        public virtual void Reset()
        {
            tickCounter = 0;
            cachedIntent = NavigationIntent.None;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return NavigationIntent.None;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
        }

        protected abstract NavigationIntent BuildIntent(AIContext ctx);

        protected Vector2 Steered(Vector2 planePos, Vector2 commandedVel) =>
            ArchetypeSteering.BorderTangentSteer(planePos, commandedVel, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
    }
}

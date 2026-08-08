using System;
using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Editor-authorable scripted opponent: composes the authored archetype against the context's tracked enemy on first Decide and re-composes whenever that enemy changes, so an archetype the training roster draws per episode can be flown against a player in a live sector. Shape params are authored rather than jittered, and the border circle anchors on the compose-time position — a live sector has no arena to steer off.</summary>
    [Serializable]
    public sealed class LiveArchetypeChooser : IIntentChooser
    {
        [Tooltip("Which archetype to fly. Aggressor and Kiter hold a range and fire, Orbiter circles and fires, Evader flees and never fires, Dummy sits still.")]
        [SerializeField] private OpponentArchetype archetype = OpponentArchetype.Aggressor;

        [Tooltip("Aggressor/Kiter: the range held on the line of sight. The roster draws 8-12 for the Aggressor and 14-18 for the Kiter; the laser envelope is 20 u.")]
        [SerializeField] private float desiredRange = 10f;

        [Tooltip("Caps the velocity law at this fraction of the airframe's max speed. The roster draws 0.7-1.0, or 0.4-0.6 for the Orbiter — above ~0.6 the orbit slides outside the laser envelope.")]
        [SerializeField] private float speedFraction = 0.85f;

        [Tooltip("Evader: seconds between juke flips. The roster draws 0.6-1.8.")]
        [SerializeField] private float jukePeriodSeconds = 1.2f;

        [Tooltip("Evader: the seed its juke flip sequence runs on.")]
        [SerializeField] private int jukeSeed = 1;

        [Tooltip("Orbiter: the circling radius. The roster draws 14-18.")]
        [SerializeField] private float orbitRadius = 16f;

        [Tooltip("Orbiter: circling direction — positive circles counter-clockwise.")]
        [SerializeField] private int orbitDirection = 1;

        [Tooltip("Radius of the border circle the archetype tangent-steers off, centered on the compose-time position. Author a huge value for no border.")]
        [SerializeField] private float borderRadius = 500f;

        private Ship self;
        private Ship enemy;
        private IIntentChooser inner;
        private Vector2 borderCenter;
        private bool reanchorBorder;

        public BrainDecision? Decide(AIContext ctx, float dt)
        {
            if (!self)
                Compose(ctx);

            // Deferred to the first post-reset tick: Reset fires before a respawn teleport lands.
            if (reanchorBorder)
            {
                borderCenter = self.Kinematics.pos;
                reanchorBorder = false;
                // The archetypes bind the border at Configure, so a moved anchor needs a fresh chooser.
                inner = null;
            }

            Retarget(ctx.Combat.Enemy);
            return inner?.Decide(ctx, dt);
        }

        public void Reset()
        {
            inner?.Reset();
            reanchorBorder = true;
        }

        private void Retarget(Ship next)
        {
            if (inner != null && next == enemy) return;

            enemy = next;
            // Every archetype but the Dummy binds its target at Configure and has nothing to fly without one.
            inner = !enemy && archetype != OpponentArchetype.Dummy
                ? null
                : ArchetypeChoosers.Create(archetype, Shape(), enemy, jukeSeed,
                    borderCenter, borderRadius);
        }

        private OpponentDraw Shape() => new()
        {
            speedFraction = speedFraction,
            jukePeriod = jukePeriodSeconds,
            orbitRadius = orbitRadius,
            orbitDirection = orbitDirection,
            desiredRange = desiredRange,
        };

        private void Compose(AIContext ctx)
        {
            self = ctx.Self as Ship;
            if (!self)
                throw new InvalidOperationException("LiveArchetypeChooser requires a Ship context.");

            borderCenter = self.Kinematics.pos;
        }
    }
}

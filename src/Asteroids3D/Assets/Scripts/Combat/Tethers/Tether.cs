using System;
using Asteroids;
using Substrate;
using UnityEngine;
using Utils;

namespace Combat.Tethers
{
    public enum TetherEnd { None, TimedOut, Lost, Cut, Snapped }

    /// <summary>The body a tether holds, and the epoch that identifies that body after a pool recycle.</summary>
    public readonly struct HookTarget
    {
        public readonly Rigidbody Body;
        private readonly AsteroidDamage asteroid;
        private readonly int epoch;

        public HookTarget(Rigidbody body, AsteroidDamage asteroid)
        {
            Body = body;
            this.asteroid = asteroid;
            epoch = asteroid ? asteroid.SpawnEpoch : 0;
        }

        // A recycled asteroid keeps its rigidbody, so the epoch must match.
        public bool Alive =>
            Body && Body.gameObject.activeInHierarchy && (!asteroid || asteroid.SpawnEpoch == epoch);
    }

    /// <summary>
    /// A rope from a shooter's nose to a hook point. Each step pulls both ends by force and reports why it ended.
    /// Design on arc #1526.
    /// </summary>
    public sealed class Tether
    {
        private static readonly LayerMask AsteroidMask = LayerIds.Mask(LayerIds.Asteroid);

        private readonly Rigidbody shooter;
        private readonly Transform nose;
        private readonly HookTarget target;
        private readonly Vector3 localHook;
        private float elapsed;

        public Tether(Rigidbody shooter, Transform nose, HookTarget target, Vector3 worldHookPoint)
        {
            if (!shooter) throw new ArgumentNullException(nameof(shooter));
            if (!nose) throw new ArgumentNullException(nameof(nose));
            if (!target.Body) throw new ArgumentNullException(nameof(target));

            this.shooter = shooter;
            this.nose = nose;
            this.target = target;
            localHook = target.Body.transform.InverseTransformPoint(worldHookPoint);
            Length = Vector3.Distance(NosePoint, HookPoint);
        }

        public Vector3 NosePoint => GamePlane.WorldPointOnPlane(nose.position);
        public Vector3 HookPoint => GamePlane.WorldPointOnPlane(target.Body.transform.TransformPoint(localHook));
        public float Length { get; private set; }

        public TetherEnd Step(float dt, in TetherSpec spec)
        {
            elapsed += dt;
            if (elapsed >= spec.MaxSeconds) return TetherEnd.TimedOut;
            if (!shooter || !shooter.gameObject.activeInHierarchy || !target.Alive) return TetherEnd.Lost;
            if (!LineOfSight.IsClear(NosePoint, HookPoint, target.Body.transform, AsteroidMask)) return TetherEnd.Cut;

            Length = TetherLaw.Reel(Length, spec.ReelRate, spec.MinLength, dt);

            var nosePoint = NosePoint;
            var hookPoint = HookPoint;
            var offset = hookPoint - nosePoint;
            var distance = offset.magnitude;
            if (distance <= 0f) return TetherEnd.None;

            var dir = offset / distance;
            var separation = Vector3.Dot(
                target.Body.GetPointVelocity(hookPoint) - shooter.GetPointVelocity(nosePoint), dir);
            var tension = TetherLaw.Tension(distance, Length, separation, spec.Stiffness, spec.Damping);
            if (tension > spec.SnapForce) return TetherEnd.Snapped;
            if (tension > 0f)
            {
                target.Body.AddForceAtPosition(-dir * tension, hookPoint, ForceMode.Force);
                shooter.AddForceAtPosition(dir * tension, nosePoint, ForceMode.Force);
            }

            return TetherEnd.None;
        }
    }
}

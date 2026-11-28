using System;
using Balance;
using Combat.Targeting;
using Loot;
using Ships.Command;
using Substrate;
using UnityEngine;
using Utils;

namespace Ships.Mods
{
    /// <summary>The Lie in wait passive on the ship that holds it: held still beside a heavy asteroid long enough, the ship conceals itself until it thrusts or fires.</summary>
    [Serializable]
    public sealed class LieInWait
    {
        // Flight assist braking a ship that is already still stays under this.
        private const float DeliberateThrottle = 0.5f;

        [Tooltip("The Lie in wait passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Seconds the ship must hold still beside a heavy asteroid before it hides.")]
        [Stat, SerializeField, Min(0f)] private float holdSeconds = 1f;

        [Tooltip("Speed (m/s) at or under which the ship counts as holding still.")]
        [Stat, SerializeField, Min(0f)] private float stillSpeed = 1.5f;

        [Tooltip("An asteroid counts as heavy at this multiple of the ship's mass or more.")]
        [Stat, SerializeField, Min(0f)] private float heavyMassRatio = 4f;

        [Tooltip("Gap (m) past the ship's radius within which an asteroid counts as beside it.")]
        [Stat, SerializeField, Min(0f)] private float contactGap = 2f;

        private float stillFor;
        private bool fired;

        public bool Held { get; private set; }
        public bool Hiding { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        public void NotifyFired(WeaponSlot _) => fired = true;

        public void Step(float dt, Vector3 position, float radius, float mass, Vector2 velocity, in PilotCommand command,
            LockChannel sight)
        {
            var broke = fired || Mathf.Abs(command.thrust) >= DeliberateThrottle || Mathf.Abs(command.strafe) >= DeliberateThrottle;
            fired = false;
            if (Hiding)
            {
                if (broke) Show(sight);
                return;
            }

            var waiting = !broke && velocity.magnitude <= stillSpeed && BesideHeavyRock(position, radius, mass);
            stillFor = waiting ? stillFor + dt : 0f;
            if (stillFor < holdSeconds) return;
            Hiding = true;
            sight.Conceal();
        }

        public void Reset(LockChannel sight)
        {
            fired = false;
            if (Hiding) Show(sight);
        }

        private void Show(LockChannel sight)
        {
            Hiding = false;
            stillFor = 0f;
            sight.Reveal();
        }

        private bool BesideHeavyRock(Vector3 position, float radius, float mass)
        {
            var overlaps = PhysicsBuffers.GetColliderBuffer();
            var count = Physics.OverlapSphereNonAlloc(position, radius + contactGap, overlaps,
                1 << LayerIds.Asteroid, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var body = overlaps[i].attachedRigidbody;
                if (body && body.mass >= heavyMassRatio * mass) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float holdSeconds, float stillSpeed, float heavyMassRatio, float contactGap)
        {
            this.key = key;
            this.holdSeconds = holdSeconds;
            this.stillSpeed = stillSpeed;
            this.heavyMassRatio = heavyMassRatio;
            this.contactGap = contactGap;
        }
#endif
    }
}

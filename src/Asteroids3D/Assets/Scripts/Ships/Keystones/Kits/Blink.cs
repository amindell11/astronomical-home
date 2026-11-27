using System.Collections.Generic;
using Balance;
using Combat.Targeting;
using Movement;
using Ships.Mods;
using Substrate;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Crimson's kit: a jump toward the pilot's target point, clamped to range, keeping the ship's
    /// velocity. A destination inside a rock or a ship shortens to the farthest clear point on the
    /// jump line. It pays when a missile tracking the ship loses it right after the jump. With Recall,
    /// the jump leaves a marker at its start, and a fresh press soon after jumps back to it, cooldown or not.
    /// </summary>
    public sealed class Blink : Keystone
    {
        private const int ClearanceSteps = 12;
        private const float TellSeconds = 0.25f;
        private const float PaidSeconds = 0.25f;

        [Tooltip("Longest jump, in units.")]
        [Stat, SerializeField] private float range = 12f;

        [Tooltip("Shortest plan-end distance at which an AI thinks a jump is worth it.")]
        [Stat, SerializeField] private float sightMinimum = 6f;

        [Tooltip("The Recall keystone mod, whose holding lets a second press jump back to where the blink started.")]
        [SerializeField] private KeystoneMod recall;

        [Tooltip("With Recall, seconds after a blink during which a fresh press jumps back to its start.")]
        [Stat, SerializeField] private float recallWindow = 3f;

        private int blockers;
        private Vector2? marker;
        private bool releasedSinceJump;
        private readonly Collider[] overlap = new Collider[8];
        private readonly List<ITrackingMissile> trackedAtJump = new();

        private void Awake() => blockers = LayerMask.GetMask("Ship", "ShipBody", "Asteroid");

        public override string DisplayName => "Blink";
        public float Range => range;
        public Vector2? Marker => marker;
        protected override bool TakesPressOnCooldown => marker.HasValue && SinceActivation <= recallWindow;
        public override bool Active => SinceActivation < TellSeconds;

        public override bool ShouldActivate(in KeystoneContext context) =>
            context.Destination is { } end && Vector2.Distance(end, context.Self.pos) >= sightMinimum;

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            trackedAtJump.Clear();
            trackedAtJump.AddRange(Host.Lock.Trackers);
            marker = Behaviours.Holds(recall) ? kinematics.pos : (Vector2?)null;
            releasedSinceJump = false;
            var aim = target ?? kinematics.pos + kinematics.Forward * range;
            return new KeystoneEffect(Vector2.zero, ClearJump(kinematics.pos, Vector2.ClampMagnitude(aim - kinematics.pos, range)));
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (trackedAtJump.Count == 0 || SinceActivation > PaidSeconds) return default;
            foreach (var missile in trackedAtJump)
            {
                if (!LostTrack(missile)) continue;
                CreditActivation();
                trackedAtJump.Clear();
                return default;
            }
            return default;
        }

        // The press that blinked can stay held past its step, so only a press after a release recalls.
        protected override KeystoneEffect Idle(in Kinematics kinematics, float input)
        {
            if (marker is not { } start) return default;
            if (SinceActivation > recallWindow)
            {
                marker = null;
                return default;
            }
            if (input == 0f)
            {
                releasedSinceJump = true;
                return default;
            }
            if (!releasedSinceJump) return default;
            marker = null;
            return new KeystoneEffect(Vector2.zero, ClearJump(kinematics.pos, start - kinematics.pos));
        }

        protected override void OnReset() => marker = null;

        // Still flying but no longer tracking this ship; a missile that hit or expired has despawned.
        private bool LostTrack(ITrackingMissile missile)
        {
            var trackers = Host.Lock.Trackers;
            for (var i = 0; i < trackers.Count; i++)
                if (ReferenceEquals(trackers[i], missile)) return false;
            return missile is Component flying && flying && flying.gameObject.activeInHierarchy;
        }

        private Vector2 ClearJump(Vector2 from, Vector2 jump)
        {
            for (var i = ClearanceSteps; i > 0; i--)
            {
                var candidate = jump * (i / (float)ClearanceSteps);
                if (IsClear(from + candidate)) return candidate;
            }
            return Vector2.zero;
        }

        private bool IsClear(Vector2 planePoint)
        {
            var count = Physics.OverlapSphereNonAlloc(GamePlane.PlanePointToWorld(planePoint), Host.Radius, overlap,
                blockers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (overlap[i].attachedRigidbody != Host.Body) return false;
            return true;
        }
    }
}

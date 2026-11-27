using Balance;
using Movement;
using Ships.Mods;
using Substrate;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Valis's kit raises speed, forward thrust, turning and lateral thrust during its active window.
    /// The ship reads the scales live in its physics and the dynamics the solver plans with;
    /// after the window the cap bleeds back down, so the speed it built is the kit's cost to shed.
    /// Two prototype mods change the window's flight: Snap turn swings the whole velocity onto the nose
    /// as the window ends, and Orbit bends the course around a rock the pilot turns toward while
    /// skimming it, keeping speed.
    /// </summary>
    public sealed class Overdrive : Keystone
    {
        [Tooltip("Seconds after activation during which speed, thrust and maneuverability stay raised.")]
        [Stat, SerializeField] private float window = 3f;

        [Tooltip("Multiplier on the speed cap during the window.")]
        [Stat, SerializeField] private float speedScale = 1.35f;

        [Tooltip("Multiplier on forward thrust during the window; must lift drag-limited top speed past the raised cap.")]
        [Stat, SerializeField] private float thrustScale = 1.5f;

        [Tooltip("Multiplier on yaw torque, maximum yaw rate and lateral thrust during the window.")]
        [Stat, SerializeField] private float maneuverabilityScale = 1.5f;

        [Tooltip("Fraction of the cap the navigator must ask for before an AI fires.")]
        [Stat, SerializeField] private float sightSaturation = 0.8f;

        [Tooltip("The Snap turn keystone mod, whose holding swings the velocity onto the nose as the window ends.")]
        [SerializeField] private KeystoneMod snapTurn;

        [Tooltip("The Orbit keystone mod, whose holding bends the course around a skimmed rock during the window.")]
        [SerializeField] private KeystoneMod orbit;

        [Tooltip("With Orbit, the widest gap, in units, between the hull and a rock's surface at which the course bends around it.")]
        [Stat, SerializeField] private float orbitReach = 4f;

        [Tooltip("With Orbit, the smallest rock radius, in units, the course bends around.")]
        [Stat, SerializeField] private float orbitMinRadius = 2f;

        private readonly Collider[] nearby = new Collider[16];
        private bool windowOpen;

        public override string DisplayName => "Overdrive";
        public float Window => window;
        public override bool Active => SinceActivation < window;
        public override float SpeedScale => Active ? speedScale : 1f;
        public override float ThrustScale => Active ? thrustScale : 1f;
        public override float ManeuverabilityScale => Active ? maneuverabilityScale : 1f;
        public override float PeakSpeedScale => speedScale;
        public override float PeakThrustScale => thrustScale;

        // Saturated: the navigator asks for (nearly) all the speed the cap allows.
        public override bool ShouldActivate(in KeystoneContext context)
        {
            var desired = context.DesiredVelocity;
            return !float.IsNaN(desired.x) && desired.magnitude >= sightSaturation * context.SelfMaxSpeed;
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            windowOpen = true;
            return default;
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (Active)
            {
                if (kinematics.Speed > baseMaxSpeed) CreditActivation();
                return Behaviours.Holds(orbit) ? Orbit(in kinematics, dt) : default;
            }
            if (!windowOpen) return default;
            windowOpen = false;
            return Behaviours.Holds(snapTurn) ? Snap(in kinematics, dt) : default;
        }

        protected override void OnReset() => windowOpen = false;

        private static KeystoneEffect Snap(in Kinematics kinematics, float dt) =>
            new(Vector2.zero, Vector2.zero, (kinematics.Forward * kinematics.Speed - kinematics.vel) / dt);

        // The course turns toward the tangent of the circle around the rock, at most the rate that
        // circle needs, and only once the rock is abeam; ahead of abeam the course already clears it.
        private KeystoneEffect Orbit(in Kinematics kinematics, float dt)
        {
            var speed = kinematics.Speed;
            if (Turn == 0f || speed < 1e-3f || !TryNearestRock(kinematics.pos, out var centre)) return default;
            var toRock = centre - kinematics.pos;
            var distance = toRock.magnitude;
            if (distance < 1e-3f || Vector2.Dot(kinematics.Forward, toRock) < 0f) return default;
            var side = Mathf.Sign(kinematics.vel.x * toRock.y - kinematics.vel.y * toRock.x);
            if (side != Mathf.Sign(Turn)) return default;

            var inward = toRock / distance;
            var tangent = side > 0f ? new Vector2(inward.y, -inward.x) : new Vector2(-inward.y, inward.x);
            var bend = Vector2.SignedAngle(kinematics.vel, tangent);
            if (Mathf.Sign(bend) != side) return default;
            var turn = side * Mathf.Min(Mathf.Abs(bend), speed * dt / distance * Mathf.Rad2Deg);
            var bent = (Vector2)(Quaternion.AngleAxis(turn, Vector3.forward) * kinematics.vel);
            return new KeystoneEffect(Vector2.zero, Vector2.zero, (bent - kinematics.vel) / dt);
        }

        private bool TryNearestRock(Vector2 pos, out Vector2 centre)
        {
            centre = default;
            var found = false;
            var nearest = float.PositiveInfinity;
            var count = Physics.OverlapSphereNonAlloc(GamePlane.PlanePointToWorld(pos), Host.Radius + orbitReach, nearby,
                1 << LayerIds.Asteroid, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var bounds = nearby[i].bounds;
                var rock = GamePlane.WorldPointToPlane(bounds.center);
                var extents = GamePlane.WorldDirToPlane(bounds.extents);
                if ((Mathf.Abs(extents.x) + Mathf.Abs(extents.y)) * 0.5f < orbitMinRadius) continue;
                var distance = (rock - pos).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                centre = rock;
                found = true;
            }
            return found;
        }
    }
}

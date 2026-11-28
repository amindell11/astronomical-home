using Balance;
using Movement;
using Ships.Mods;
using Substrate;
using UnityEngine;
using Utils;
using Ships.Presentation;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Valis's kit raises speed, forward thrust, turning and lateral thrust during its active window.
    /// The ship reads the scales live in its physics and the dynamics the solver plans with;
    /// after the window the cap bleeds back down, so the speed it built is the kit's cost to shed.
    /// Orbit makes rocks a speed source: each rock skimmed, in or out of the window, adds a stack of
    /// speed and thrust until the stacks lapse; inside the window the hull passes through rocks and the
    /// course bends around one the pilot turns toward. Relentless makes the window a charge: forward
    /// thrust grows as the nose points at the nearest enemy and shrinks as it points away.
    /// </summary>
    public sealed class Overdrive : Keystone
    {
        private static readonly Color OrbitInk = new(1f, .7f, .3f);

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

        [Tooltip("The Orbit keystone mod, whose holding turns skimmed rocks into speed and lets the window pass through rocks.")]
        [SerializeField] private KeystoneMod orbit;

        [Tooltip("With Orbit, the widest gap, in units, between the hull and a rock's surface at which the course bends around it.")]
        [Stat, SerializeField] private float orbitReach = 4f;

        [Tooltip("With Orbit, the smallest rock radius, in units, that bends the course or counts as a skim.")]
        [Stat, SerializeField] private float orbitMinRadius = 2f;

        [Tooltip("With Orbit, the widest gap, in units, between the hull and a rock's surface that counts as a skim.")]
        [Stat, SerializeField] private float skimReach = 3f;

        [Tooltip("With Orbit, the speed cap and forward thrust each skim stack adds, as a fraction.")]
        [Stat, SerializeField, Range(0f, 0.5f)] private float skimGain = 0.08f;

        [Tooltip("With Orbit, the most skim stacks held at once.")]
        [Stat, SerializeField, Min(1)] private int skimMaxStacks = 5;

        [Tooltip("With Orbit, seconds away from every rock before all skim stacks lapse.")]
        [Stat, SerializeField] private float skimHold = 2f;

        [Tooltip("The Relentless keystone mod, whose holding strengthens thrust toward the nearest enemy during the window and weakens it away.")]
        [SerializeField] private KeystoneMod relentless;

        [Tooltip("With Relentless, how far forward thrust swings: it scales by 1 plus this times the cosine between the nose and the nearest enemy.")]
        [Stat, SerializeField, Range(0f, 1f)] private float relentlessGain = 0.5f;

        [Tooltip("With Relentless, the farthest an enemy may be, in units, to count as the nearest.")]
        [Stat, SerializeField] private float relentlessRange = 60f;

        private readonly Collider[] nearby = new Collider[16];
        private float charge = 1f;
        private int skims;
        private Collider skimmed;
        private float sinceSkim;

        public override string DisplayName => "Overdrive";
        public float Window => window;
        public override bool Active => SinceActivation < window;
        public override float SpeedScale => (Active ? speedScale : 1f) * SkimScale;
        public override float ThrustScale => (Active ? thrustScale * charge : 1f) * SkimScale;
        public override float ManeuverabilityScale => Active ? maneuverabilityScale : 1f;
        public override bool PassesRocks => Active && Behaviours.Holds(orbit);
        public override float PeakSpeedScale => speedScale * PeakSkimScale;
        public override float PeakThrustScale =>
            thrustScale * (Behaviours.Holds(relentless) ? 1f + relentlessGain : 1f) * PeakSkimScale;
        public int SkimStacks => skims;
        private float SkimScale => 1f + skimGain * skims;
        private float PeakSkimScale => Behaviours.Holds(orbit) ? 1f + skimGain * skimMaxStacks : 1f;

        // Saturated: the navigator asks for (nearly) all the speed the cap allows.
        public override bool ShouldActivate(in KeystoneContext context)
        {
            var desired = context.DesiredVelocity;
            return !float.IsNaN(desired.x) && desired.magnitude >= sightSaturation * context.SelfMaxSpeed;
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target) => default;

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            charge = Active && Behaviours.Holds(relentless) ? Charge(in kinematics) : 1f;
            var orbiting = Behaviours.Holds(orbit);
            Skim(in kinematics, dt, orbiting);
            if (!Active) return default;
            if (kinematics.Speed > baseMaxSpeed) CreditActivation();
            return orbiting ? Orbit(in kinematics, dt) : default;
        }

        protected override void OnReset()
        {
            charge = 1f;
            skims = 0;
            skimmed = null;
            sinceSkim = 0f;
        }

        // Each new nearest rock in reach adds a stack; staying in reach holds them, leaving every rock lets them lapse.
        private void Skim(in Kinematics kinematics, float dt, bool orbiting)
        {
            if (!orbiting)
            {
                skims = 0;
                skimmed = null;
                return;
            }
            var near = TryNearestRock(kinematics.pos, skimReach, out _, out var rock);
            if (near && rock != skimmed)
            {
                var stacked = Mathf.Min(skims + 1, skimMaxStacks);
                if (stacked > skims) Pop(new Word($"ORBIT x{stacked}", OrbitInk), Body.worldCenterOfMass);
                skims = stacked;
            }
            skimmed = near ? rock : null;
            sinceSkim = near ? 0f : sinceSkim + dt;
            if (sinceSkim >= skimHold) skims = 0;
        }

        private float Charge(in Kinematics kinematics)
        {
            if (!TryNearestEnemy(kinematics.pos, out var enemy)) return 1f;
            var toward = enemy - kinematics.pos;
            if (toward.sqrMagnitude < 1e-6f) return 1f;
            return 1f + relentlessGain * Vector2.Dot(kinematics.Forward, toward.normalized);
        }

        // A concealed enemy is hidden from the charge as from every other sensor.
        private bool TryNearestEnemy(Vector2 pos, out Vector2 at)
        {
            at = default;
            var found = false;
            var nearest = float.PositiveInfinity;
            var hulls = PhysicsBuffers.GetColliderBuffer(64);
            var count = Physics.OverlapSphereNonAlloc(GamePlane.PlanePointToWorld(pos), relentlessRange, hulls,
                1 << LayerIds.Ship, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (!TryEnemy(hulls[i], out var enemy) || enemy.Lock.IsConcealed) continue;
                var enemyPos = GamePlane.WorldPointToPlane(enemy.Rigidbody.worldCenterOfMass);
                var distance = (enemyPos - pos).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                at = enemyPos;
                found = true;
            }
            return found;
        }

        // The course turns toward the tangent of the circle around the rock, at most the rate that
        // circle needs, and only once the rock is abeam; ahead of abeam the course already clears it.
        private KeystoneEffect Orbit(in Kinematics kinematics, float dt)
        {
            var speed = kinematics.Speed;
            if (Turn == 0f || speed < 1e-3f || !TryNearestRock(kinematics.pos, orbitReach, out var centre, out _)) return default;
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

        public override bool CountsForOrbit(Collider rock) => Behaviours.Holds(orbit) && BigEnoughToOrbit(rock.bounds);

        private bool BigEnoughToOrbit(Bounds bounds)
        {
            var extents = GamePlane.WorldDirToPlane(bounds.extents);
            return (Mathf.Abs(extents.x) + Mathf.Abs(extents.y)) * 0.5f >= orbitMinRadius;
        }

        private bool TryNearestRock(Vector2 pos, float reach, out Vector2 centre, out Collider rock)
        {
            centre = default;
            rock = null;
            var found = false;
            var nearest = float.PositiveInfinity;
            var count = Physics.OverlapSphereNonAlloc(GamePlane.PlanePointToWorld(pos), Host.Radius + reach, nearby,
                1 << LayerIds.Asteroid, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var bounds = nearby[i].bounds;
                if (!BigEnoughToOrbit(bounds)) continue;
                var at = GamePlane.WorldPointToPlane(bounds.center);
                var distance = (at - pos).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                centre = at;
                rock = nearby[i];
                found = true;
            }
            return found;
        }
    }
}

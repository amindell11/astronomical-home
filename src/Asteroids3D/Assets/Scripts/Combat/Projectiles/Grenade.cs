using Balance;
using System;
using System.Collections.Generic;
using System.Text;
using Damage;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    /// <summary>
    /// Concussion charge flown as a reverse missile: launched at a fixed speed straight at its
    /// target point (clamped to <see cref="ProjectileBase.MaxDistance"/>), it brakes by its own
    /// thrust to rest on that point. Being shot detonates it at once — the owner's fire included.
    /// Every other fuse waits for <see cref="armingTime"/> after launch: then it detonates on
    /// arrival (holding on the point if it got there first), on contact, or when a ship comes
    /// within <see cref="proximityRadius"/> — never the owner's hull. Detonation spawns a
    /// <see cref="ConcussionWave"/>; the charge itself never applies damage. Design: arc #950.
    /// </summary>
    public class Grenade : Projectile<Grenade>, IDamageable, ITransientSpawner, IChargeFlight
    {
        [Header("Charge")]
        [Stat, SerializeField, Min(0.01f)] private float launchSpeed = 40f;

        [Header("Fuse")]
        [Tooltip("Seconds after launch before arrival, contact or proximity can detonate the charge; being shot always does.")]
        [Stat, SerializeField, Min(0f)] internal float armingTime = 0.6f;
        [Tooltip("An armed charge detonates when any ship but its owner's comes within this distance.")]
        [Stat, SerializeField, Min(0f)] internal float proximityRadius = 4f;

        [Header("Blast")]
        [Stat, SerializeField] private ConcussionWave wavePrefab;

        private Vector3 targetPoint;
        private Vector3 heading;
        private bool detonated;
        private float sinceLaunch;
        private int shipLayers;
        private string dbgProx;
        internal float DbgSinceLaunch => sinceLaunch;

        private bool Armed => sinceLaunch >= armingTime;

        protected override DamageKind Kind => DamageKind.ConcussionWave;
        protected override bool TakesOwnerFire => true;

        public event Action<Vector3> OnDetonated;

        /// <summary>Announces the detonation's wave so whoever tracks this grenade tracks the wave too (<see cref="ITransientSpawner"/>).</summary>
        public event Action<MonoBehaviour, Action> Spawned;

        /// <summary>Where this charge comes to rest: the launch's target point, clamped to max range.</summary>
        public Vector3 TargetPoint => targetPoint;

        public float LaunchSpeed => launchSpeed;

        /// <summary>Serialized-state read for hangar stat lines (evaluated on the prefab asset).</summary>
        public ConcussionWave WavePrefab => wavePrefab;

        event Action IChargeFlight.Ended
        {
            add => ReturnedToPool += value;
            remove => ReturnedToPool -= value;
        }

        protected override void Awake()
        {
            base.Awake();
            shipLayers = LayerIds.Mask(LayerIds.Ship);
            SimplePool<ConcussionWave>.Warm(wavePrefab);
        }

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {
            var origin = GamePlane.WorldPointToPlane(transform.position);
            var offset = GamePlane.WorldPointToPlane(targetPoint) - origin;
            var planarHeading = offset.sqrMagnitude > 0.0001f ? offset.normalized : GamePlane.WorldDirToPlane(direction).normalized;

            heading = GamePlane.PlaneDirToWorld(planarHeading);
            this.targetPoint = GamePlane.PlanePointToWorld(origin + planarHeading * Mathf.Min(offset.magnitude, maxDistance));
            rb.linearVelocity = heading * launchSpeed;
            base.Launch(direction, targetPoint);
            SelfHitDebug.LiveCharges.Add(this);
            var shooterShip = Shooter as global::Ships.Ship;
            SelfHitDebug.Log($"launch charge={GetInstanceID()} origin={SelfHitDebug.P(transform.position)} req={SelfHitDebug.P(targetPoint)} clamped={SelfHitDebug.P(this.targetPoint)} lobDist={Vector3.Distance(transform.position, this.targetPoint):F2} dTargetNow={SelfHitDebug.Dist(transform.position, SelfHitDebug.Target):F2} shooter[{SelfHitDebug.ShipState(shooterShip)}] target[{SelfHitDebug.ShipState(SelfHitDebug.Target)}] liveCharges={SelfHitDebug.LiveCharges.Count} liveWaves={SelfHitDebug.LiveWaves.Count}");
        }

        protected override void FixedUpdate()
        {
            sinceLaunch += Time.fixedDeltaTime;
            var remaining = Vector3.Dot(targetPoint - transform.position, heading);
            var closingSpeed = Vector3.Dot(rb.linearVelocity, heading);
            var arrived = remaining <= 0f || closingSpeed <= 0f;
            if (Armed && (arrived || ShipInProximity()))
            {
                Detonate(arrived ? $"arrival(remaining={remaining:F2},closing={closingSpeed:F2})" : "proximity:" + dbgProx);
                return;
            }

            if (arrived)
                rb.linearVelocity = Vector3.zero;
            else
                rb.AddForce(-heading * BrakingDeceleration(closingSpeed, remaining, Time.fixedDeltaTime), ForceMode.Acceleration);
            base.FixedUpdate();
        }

        // Discrete-step form of v²/2d, which stops about v·dt/2 short; re-solved each step it holds constant.
        internal static float BrakingDeceleration(float speed, float distance, float dt) =>
            speed * speed / (2f * distance + speed * dt);

        private bool ShipInProximity()
        {
            var buffer = PhysicsBuffers.GetColliderBuffer();
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, proximityRadius, buffer, shipLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
                if (TryGetHitTarget(buffer[i], out _))
                {
                    dbgProx = $"{buffer[i].name}(root={buffer[i].transform.root.name},d={Vector3.Distance(transform.position, buffer[i].ClosestPoint(transform.position)):F2})";
                    return true;
                }
            return false;
        }

        protected override void OnHit(IDamageable other)
        {
            if (!Armed) return;
            RaiseHit(other);
            Detonate("contact:" + other.gameObject.name);
        }

        public float TakeDamage(in DamageInfo hit)
        {
            Detonate($"shot(kind={hit.Kind},attacker={hit.AttackerId},amt={hit.Amount:F1},at={SelfHitDebug.P(hit.HitPoint)})");
            return 0f;
        }

        private void Detonate(string cause)
        {
            if (detonated) return;
            detonated = true;

            var shooterShip = Shooter as global::Ships.Ship;
            SelfHitDebug.Log($"detonate charge={GetInstanceID()} cause={cause} sinceLaunch={sinceLaunch:F2} armed={Armed} pos={SelfHitDebug.P(transform.position)} targetPoint={SelfHitDebug.P(targetPoint)} offTargetPoint={Vector3.Distance(transform.position, targetPoint):F2} speed={rb.linearVelocity.magnitude:F2} dShooter={SelfHitDebug.Dist(transform.position, shooterShip):F2} dTarget={SelfHitDebug.Dist(transform.position, SelfHitDebug.Target):F2} shooter[{SelfHitDebug.ShipState(shooterShip)}] target[{SelfHitDebug.ShipState(SelfHitDebug.Target)}] liveCharges={SelfHitDebug.LiveCharges.Count} liveWaves={SelfHitDebug.LiveWaves.Count}");
            var wave = SimplePool<ConcussionWave>.Get(wavePrefab, transform.position, Quaternion.identity);
            wave.dbgChargeId = GetInstanceID();
            wave.dbgCause = cause;
            wave.dbgShooterBody = Shooter?.Body;
            wave.dbgShooter = shooterShip;
            wave.Begin(Shooter?.Id ?? Ships.Registry.ShipId.Invalid);
            Spawned?.Invoke(wave, wave.ReturnToPoolImmediate);

            OnDetonated?.Invoke(transform.position);
            Dispose();
        }

        protected override void OnReturnToPool()
        {
            if (!detonated)
                SelfHitDebug.Log($"return-undetonated charge={GetInstanceID()} sinceLaunch={sinceLaunch:F2} pos={SelfHitDebug.P(transform.position)} targetPoint={SelfHitDebug.P(targetPoint)} traveled={DistanceTraveled:F2}");
            SelfHitDebug.LiveCharges.Remove(this);
            detonated = false;
            sinceLaunch = 0f;
        }
    }

    public static class SelfHitDebug
    {
        public static float T0;
        public static string Episode = "";
        public static global::Ships.Ship Shooter;
        public static global::Ships.Ship Target;
        public static string LastBrain = "";
        public static readonly List<Grenade> LiveCharges = new();
        public static readonly List<ConcussionWave> LiveWaves = new();

        public static float T => Time.fixedTime - T0;

        public static void Log(string msg) => Debug.Log($"[DEBUG-sh] {Episode} t={T:F2} {msg}");

        public static string P(Vector3 world)
        {
            var p = GamePlane.WorldPointToPlane(world);
            return $"({p.x:F2},{p.y:F2})";
        }

        public static float Dist(Vector3 world, global::Ships.Ship ship) =>
            ship ? Vector2.Distance(GamePlane.WorldPointToPlane(world), ship.Kinematics.pos) : -1f;

        public static string ShipState(global::Ships.Ship ship)
        {
            if (!ship) return "null";
            var k = ship.Kinematics;
            return $"{ship.name} pos=({k.pos.x:F2},{k.pos.y:F2}) vel=({k.vel.x:F2},{k.vel.y:F2}) speed={k.Speed:F2} fwd=({k.Forward.x:F2},{k.Forward.y:F2}) hp={ship.Damage.Health.CurrentValue:F1}";
        }

        public static string VsTransients(Vector2 p)
        {
            var sb = new StringBuilder("landingVs:");
            foreach (var w in LiveWaves)
                if (w)
                {
                    var d = Vector2.Distance(p, GamePlane.WorldPointToPlane(w.transform.position));
                    sb.Append($" [wave={w.GetInstanceID()} dCentre={d:F2} r={w.Radius:F2} max={w.MaxRadius:F1} frontArrivesIn={(d - w.Radius) / w.DbgExpand:F2}s]");
                }
            foreach (var c in LiveCharges)
                if (c)
                    sb.Append($" [charge={c.GetInstanceID()} dCharge={Vector2.Distance(p, GamePlane.WorldPointToPlane(c.transform.position)):F2} dTargetPoint={Vector2.Distance(p, GamePlane.WorldPointToPlane(c.TargetPoint)):F2} since={c.DbgSinceLaunch:F2}]");
            return sb.ToString();
        }

        public static string Dump()
        {
            var sb = new StringBuilder();
            sb.Append($"dump shooter[{ShipState(Shooter)}] target[{ShipState(Target)}] dShooterTarget={(Shooter && Target ? Vector2.Distance(Shooter.Kinematics.pos, Target.Kinematics.pos) : -1f):F2} lastBrain[{LastBrain}]");
            foreach (var c in LiveCharges)
                if (c) sb.Append($" | charge={c.GetInstanceID()} owner={(c.Shooter as global::Ships.Ship)?.name} pos={P(c.transform.position)} tp={P(c.TargetPoint)} since={c.DbgSinceLaunch:F2} dShooter={Dist(c.transform.position, Shooter):F2}");
            foreach (var w in LiveWaves)
                if (w) sb.Append($" | wave={w.GetInstanceID()} charge={w.dbgChargeId} cause={w.dbgCause} centre={P(w.transform.position)} r={w.Radius:F2} dShooter={Dist(w.transform.position, Shooter):F2} dTarget={Dist(w.transform.position, Target):F2}");
            return sb.ToString();
        }
    }
}

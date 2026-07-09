using Combat.Projectile;
using Game;
using UnityEngine;
using Utils;

namespace AI.Scanning
{
    public enum ThreatKind
    {
        Missile = 0,
    }

    /// <summary>One in-flight threat, already projected to plane space.</summary>
    public readonly struct ThreatContact
    {
        public readonly Vector2 planePos;
        public readonly Vector2 planeVel;
        public readonly ThreatKind kind;

        public ThreatContact(Vector2 planePos, Vector2 planeVel, ThreatKind kind)
        {
            this.planePos = planePos;
            this.planeVel = planeVel;
            this.kind = kind;
        }
    }

    /// <summary>
    /// Scans the projectile layer for in-flight ordnance that threatens the observing ship.
    /// Mirrors <see cref="ShipScanner"/> (buffered non-alloc OverlapSphere), but queries trigger
    /// colliders — projectiles are triggers — and classifies by projectile type rather than
    /// registry hostility. Only guided <see cref="Missile"/>s are threat tracks today (lasers and
    /// railguns are effectively undodgeable and excluded); the design generalizes to mines later.
    /// </summary>
    public class ThreatScanner
    {
        private readonly Transform origin;
        private readonly Transform selfRoot;
        private readonly float radius;
        private readonly int layerMask;
        private readonly Collider[] colliderBuffer;
        private readonly ProjectileBase[] seen;

        public ThreatContact[] Buffer { get; }
        public int Count { get; private set; }

        public ThreatScanner(Transform origin, Transform selfRoot, float radius, int bufferSize = 32)
        {
            this.origin = origin;
            this.selfRoot = selfRoot;
            this.radius = radius;
            layerMask = LayerIds.Mask(LayerIds.Projectile);
            colliderBuffer = new Collider[bufferSize];
            seen = new ProjectileBase[bufferSize];
            Buffer = new ThreatContact[bufferSize];
        }

        public int Scan()
        {
            Count = 0;
            if (origin == null) return 0;

            var hitCount = Physics.OverlapSphereNonAlloc(
                origin.position, radius, colliderBuffer, layerMask, QueryTriggerInteraction.Collide);

            for (var i = 0; i < hitCount && Count < Buffer.Length; i++)
            {
                var col = colliderBuffer[i];
                if (!col) continue;
                if (col.GetComponentInParent<ProjectileBase>() is not Missile missile) continue;
                if (IsOwnMissile(missile) || AlreadySeen(missile)) continue;

                seen[Count] = missile;
                var body = col.attachedRigidbody;
                var worldVel = body ? body.linearVelocity : Vector3.zero;
                Buffer[Count++] = new ThreatContact(
                    GamePlane.WorldPointToPlane(missile.transform.position),
                    GamePlane.WorldDirToPlane(worldVel),
                    ThreatKind.Missile);
            }

            return Count;
        }

        private bool IsOwnMissile(Missile missile)
        {
            return missile.Shooter is Component shooter && shooter && shooter.transform.root == selfRoot;
        }

        private bool AlreadySeen(ProjectileBase projectile)
        {
            for (var i = 0; i < Count; i++)
                if (ReferenceEquals(seen[i], projectile)) return true;
            return false;
        }
    }
}

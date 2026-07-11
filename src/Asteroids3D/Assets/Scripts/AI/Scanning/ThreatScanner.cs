using Game;
using UnityEngine;
using Utils;

namespace AI.Scanning
{
    public enum ThreatKind
    {
        Missile = 0,
    }

    /// <summary>One in-flight dangerous object, in plane space.</summary>
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
    /// Sweeps the Projectile layer for live guided missiles — picked out by the "Missile" tag, so
    /// hitscan/unguided shots sharing the layer are ignored — and reports each as a plane-space
    /// kinematic contact. This is the perception channel that later lets per-weapon evasion emerge
    /// from learning rather than being authored (roadmap §3.4). Runtime-ready, but presently driven
    /// only by the editor observation gizmo — nothing on the sim tick consumes it yet.
    /// </summary>
    public class ThreatScanner
    {
        private readonly Transform origin;
        private readonly float radius;
        private readonly int mask;
        private readonly Collider[] hitBuffer;
        private readonly ThreatContact[] contacts;
        private readonly Transform[] seen;

        public ThreatContact[] Contacts => contacts;
        public int Count { get; private set; }

        public ThreatScanner(Transform origin, float radius, int bufferSize = 32)
        {
            this.origin = origin;
            this.radius = radius;
            mask = LayerIds.Mask(LayerIds.Projectile);
            hitBuffer = new Collider[bufferSize];
            contacts = new ThreatContact[bufferSize];
            seen = new Transform[bufferSize];
        }

        public void Scan()
        {
            Count = 0;
            if (!origin) return;

            var hits = Physics.OverlapSphereNonAlloc(origin.position, radius, hitBuffer, mask);
            for (var i = 0; i < hits && Count < contacts.Length; i++)
            {
                // Missiles carry their rigidbody + "Missile" tag on the collider's attached body;
                // both reads are cheap properties, so the classification stays out of hierarchy walks.
                var body = hitBuffer[i].attachedRigidbody;
                if (!body || !body.CompareTag(TagNames.Missile) || AlreadySeen(body.transform)) continue;

                seen[Count] = body.transform;
                contacts[Count++] = new ThreatContact(
                    GamePlane.WorldPointToPlane(body.position),
                    GamePlane.WorldDirToPlane(body.linearVelocity),
                    ThreatKind.Missile);
            }
        }

        private bool AlreadySeen(Transform t)
        {
            for (var i = 0; i < Count; i++)
                if (seen[i] == t) return true;
            return false;
        }
    }
}

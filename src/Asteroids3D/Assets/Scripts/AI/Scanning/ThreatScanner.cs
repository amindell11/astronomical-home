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
    /// Sweeps the Missile layer for live guided ordnance and reports each as a plane-space
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
            mask = LayerIds.Mask(LayerIds.Missile);
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
                var col = hitBuffer[i];
                var missile = col.GetComponentInParent<Missile>();
                if (!missile || AlreadySeen(missile.transform)) continue;

                var body = col.attachedRigidbody;
                var planePos = GamePlane.WorldPointToPlane(missile.transform.position);
                var planeVel = body ? GamePlane.WorldDirToPlane(body.linearVelocity) : Vector2.zero;

                seen[Count] = missile.transform;
                contacts[Count++] = new ThreatContact(planePos, planeVel, ThreatKind.Missile);
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

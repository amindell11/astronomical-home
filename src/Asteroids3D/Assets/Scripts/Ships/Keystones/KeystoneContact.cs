using Ships.Registry;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>One collision as the ship hands it to its kit.</summary>
    public readonly struct KeystoneContact
    {
        public readonly ShipId Self;
        public readonly float SelfMass;
        public readonly Vector3 SelfVelocity;
        public readonly Collider Other;
        public readonly Rigidbody OtherBody;
        public readonly Vector3 Point;

        /// <summary>How square the hit is: 1 head-on along the contact normal, 0 grazing.</summary>
        public readonly float ClosingFraction;

        public KeystoneContact(ShipId self, float selfMass, Vector3 selfVelocity, Collider other, Rigidbody otherBody,
            Vector3 point, float closingFraction)
        {
            Self = self;
            SelfMass = selfMass;
            SelfVelocity = selfVelocity;
            Other = other;
            OtherBody = otherBody;
            Point = point;
            ClosingFraction = closingFraction;
        }
    }
}

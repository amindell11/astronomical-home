using UnityEngine;

namespace Combat
{
    /// <summary>Entity that fired a shot: origin identity for self-hit checks plus velocity inheritance.</summary>
    public interface IShooter
    {
        GameObject gameObject { get; }

        Transform transform { get; }

        Vector3 Velocity { get; }

        /// <summary>Self-hit identity anchor: the entity's rigidbody, immune to hierarchy above it.</summary>
        Rigidbody Body { get; }
    }
}

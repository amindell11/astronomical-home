using UnityEngine;

/// <summary>
/// Represents an entity that can fire projectiles.
/// Provides a consistent way for projectiles to get information about their origin,
/// such as velocity for inheritance and a game object for self-hit checks.
/// </summary>
public interface IShooter
{
    /// <summary>The GameObject of the entity that fired.</summary>
    GameObject gameObject { get; }

    /// <summary>The Transform of the entity that fired.</summary>
    Transform transform { get; }

    /// <summary>The current velocity of the shooter, typically from its Rigidbody.</summary>
    Vector3 Velocity { get; }
} 
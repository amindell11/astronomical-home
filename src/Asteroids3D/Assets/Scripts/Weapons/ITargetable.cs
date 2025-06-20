using UnityEngine;

/// <summary>
/// Marker interface for anything a missile can chase.
/// Ships, Asteroids, etc. should implement this.
/// </summary>
public interface ITargetable 
{ 
    /// <summary>The point that missiles should aim for on this target.</summary>
    Transform TargetPoint { get; } 

    /// <summary>Lock-on indicator component attached to this target (may be null).</summary>
    LockOnIndicator Indicator { get; }
} 
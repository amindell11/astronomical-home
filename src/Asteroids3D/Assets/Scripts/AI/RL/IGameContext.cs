using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interface that provides game context information needed by RL agents.
/// This decouples agents from specific implementations like ArenaInstance,
/// allowing them to work in both training and gameplay scenarios.
/// </summary>
public interface IGameContext
{
    /// <summary>
    /// The center point of the play area for distance calculations.
    /// </summary>
    Vector3 CenterPosition { get; }
    
    /// <summary>
    /// The size/radius of the play area for normalization.
    /// </summary>
    float AreaSize { get; }
    
    /// <summary>
    /// All active ships in the current game context.
    /// Used for enemy detection and tactical awareness.
    /// </summary>
    IReadOnlyList<Ship> ActiveShips { get; }
    
    /// <summary>
    /// Whether the current episode/game session is active.
    /// When false, agents should not process actions or collect fresh observations.
    /// </summary>
    bool IsActive { get; }
} 
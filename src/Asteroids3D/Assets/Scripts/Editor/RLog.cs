using System.Diagnostics;
using UnityEngine;

// Lightweight compile-time stripped logging wrapper.
// Calls are kept in the editor or development builds and are a complete no-op elsewhere.
public static class RLog
{
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, Object context = null)
    {
        if (context == null)
            UnityEngine.Debug.Log(message);
        else
            UnityEngine.Debug.Log(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message, Object context = null)
    {
        if (context == null)
            UnityEngine.Debug.LogWarning(message);
        else
            UnityEngine.Debug.LogWarning(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message, Object context = null)
    {
        if (context == null)
            UnityEngine.Debug.LogError(message);
        else
            UnityEngine.Debug.LogError(message, context);
    }

    /* ───────────────────────── Channels ───────────────────────── */

    [Conditional("LOG_WEAPON")]
    public static void Weapon(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_WEAPON")]
    public static void WeaponWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_WEAPON")]
    public static void WeaponError(object message, Object context = null) => LogError(message, context);

    [Conditional("LOG_RL")]
    public static void RL(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_RL")]
    public static void RLWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_RL")]
    public static void RLError(object message, Object context = null) => LogError(message, context);
    
    [Conditional("LOG_AI")]
    public static void AI(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_AI")]
    public static void AIWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_AI")]
    public static void AIError(object message, Object context = null) => LogError(message, context);

    [Conditional("LOG_DAMAGE")]
    public static void Damage(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_DAMAGE")]
    public static void DamageWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_DAMAGE")]
    public static void DamageError(object message, Object context = null) => LogError(message, context);
    
    [Conditional("LOG_ASTEROID")]
    public static void Asteroid(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_ASTEROID")]
    public static void AsteroidWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_ASTEROID")]
    public static void AsteroidError(object message, Object context = null) => LogError(message, context);

    [Conditional("LOG_SHIP")]
    public static void Ship(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_SHIP")]
    public static void ShipWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_SHIP")]
    public static void ShipError(object message, Object context = null) => LogError(message, context);

    [Conditional("LOG_CORE")]
    public static void Core(object message, Object context = null) => Log(message, context);
    [Conditional("LOG_CORE")]
    public static void CoreWarning(object message, Object context = null) => LogWarning(message, context);
    [Conditional("LOG_CORE")]
    public static void CoreError(object message, Object context = null) => LogError(message, context);
} 
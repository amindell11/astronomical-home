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
} 
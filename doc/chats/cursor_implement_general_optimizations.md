# Implement general optimizations
_Exported on 7/2/2025 at 15:06:58 PDT from Cursor (1.1.7)_

---

**User**

implement general_Optimizations 5

---

**Cursor**

```csharp
// Create new file content
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight pool that reuses a small set of hidden <see cref="AudioSource"/> components
/// for one-shot sound playback. Replaces expensive <see cref="AudioSource.PlayClipAtPoint"/> calls
/// which allocate a temporary GameObject every time.
/// </summary>
public static class AudioOneShotPool
{
    // Queue of available AudioSources ready for reuse
    private static readonly Queue<AudioSource> s_Pool = new();

    // Root object that holds all pooled AudioSources so they persist across scene loads
    private static GameObject s_Root;

    // Helper MonoBehaviour used solely for running coroutines (e.g., to return sources)
    private class PoolRunner : MonoBehaviour {}

    private static PoolRunner Runner
    {
        get
        {
            if (s_Root == null)
            {
                s_Root = new GameObject("~AudioOneShotPool");
                Object.DontDestroyOnLoad(s_Root);
                s_Root.hideFlags = HideFlags.HideAndDontSave;
                s_Root.AddComponent<PoolRunner>();
            }
            return s_Root.GetComponent<PoolRunner>();
        }
    }

    /// <summary>
    /// Plays an <paramref name="clip"/> at <paramref name="position"/> with the given
    /// <paramref name="volume"/> using a pooled <see cref="AudioSource"/>.
    /// </summary>
    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource src = GetSource();
        src.transform.position = position;
        src.clip   = clip;
        src.volume = volume;
        src.Play();

        // Schedule return to pool once the clip is done
        Runner.StartCoroutine(ReturnAfter(src, clip.length));
    }

    private static AudioSource GetSource()
    {
        if (s_Pool.Count > 0)
        {
            AudioSource src = s_Pool.Dequeue();
            src.gameObject.SetActive(true);
            return src;
        }
        else
        {
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(Runner.transform);
            // Ensure new sources do not spatialize in 2D unless needed
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f; // full 3D (match PlayClipAtPoint default)
            return src;
        }
    }

    private static IEnumerator ReturnAfter(AudioSource src, float delay)
    {
        // WaitForSeconds uses game time, matching AudioSource playback
        yield return new WaitForSeconds(delay);
        src.Stop();
        src.clip = null;
        src.gameObject.SetActive(false);
        s_Pool.Enqueue(src);
    }
}
```

```csharp
// ... existing code ...
-            AudioSource.PlayClipAtPoint(laserSound, transform.position, laserVolume);
+            AudioOneShotPool.PlayClipAtPoint(laserSound, transform.position, laserVolume);
// ... existing code ...
```

```csharp
// ... existing code ...
-        if (explosionSound)
-            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
+        if (explosionSound)
+            AudioOneShotPool.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
// ... existing code ...
```

```csharp
// ... existing code ...
-        if (explosionSound != null)
-        {
-            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
-        }
+        if (explosionSound != null)
+        {
+            AudioOneShotPool.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
+        }
// ... existing code ...
```

Implemented General Optimization #5 – pooled one-shot audio playback:

1. Added `AudioOneShotPool` utility (`Assets/Scripts/Utils/AudioOneShotPool.cs`)
   • Maintains a small pool of hidden `AudioSource` objects.  
   • Provides `PlayClipAtPoint(AudioClip, Vector3, float)` that reuses sources and automatically returns them to the pool after the clip finishes.

2. Replaced all direct `AudioSource.PlayClipAtPoint` calls with the new pooled method:
   • `LaserProjectile.cs`
   • `ShipHealthVisuals.cs`
   • `Asteroid.cs`

This removes per-shot GameObject creation, reducing GC allocs and CPU overhead during gameplay.

---

**User**

could this use simplepool instead of reinventing the wheel


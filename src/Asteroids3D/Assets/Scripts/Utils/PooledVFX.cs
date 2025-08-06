using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PooledVFX : MonoBehaviour
{
    private ParticleSystem[] systems;
    private float cachedMaxDuration = -1f;

    private void Awake()  => systems = GetComponentsInChildren<ParticleSystem>(true);

    private void OnEnable()
    {
        // restart all systems when the object is fetched from the pool
        foreach (var ps in systems) ps.Play(true);
        
        // Cache duration calculation to avoid recalculating every time
        if (cachedMaxDuration < 0f)
        {
            CalculateMaxDuration();
        }
        
        Invoke(nameof(ReturnToPool), cachedMaxDuration);
    }
    
    private void CalculateMaxDuration()
    {
        // schedule release when the longest system finishes
        cachedMaxDuration = 0f;
        foreach (var ps in systems)
        {
            cachedMaxDuration = Mathf.Max(cachedMaxDuration,
                                        ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }

    private void ReturnToPool() => SimplePool<PooledVFX>.Release(this);
}   
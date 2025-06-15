/*using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PooledVFX : MonoBehaviour
{
    private ParticleSystem[] systems;

    private void Awake()  => systems = GetComponentsInChildren<ParticleSystem>(true);

    private void OnEnable()
    {
        // restart all systems when the object is fetched from the pool
        foreach (var ps in systems) ps.Play(true);
        // schedule release when the longest system finishes
        float maxDuration = 0f;
        foreach (var ps in systems)
            maxDuration = Mathf.Max(maxDuration,
                                    ps.main.duration + ps.main.startLifetime.constantMax);
        Invoke(nameof(ReturnToPool), maxDuration);
    }

    private void ReturnToPool() => SimplePool<PooledVFX>.Release(this);
}   */
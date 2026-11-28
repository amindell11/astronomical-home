using UnityEngine;

namespace Utils
{
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledVFX : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private float cachedMaxDuration = -1f;

        /// <summary>Takes an instance from the pool at <paramref name="scale"/>; pooled instances keep their last scale, so every caller sets one.</summary>
        public static PooledVFX Play(PooledVFX prefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var vfx = SimplePool<PooledVFX>.Get(prefab, position, rotation);
            vfx.transform.localScale = Vector3.one * scale;
            return vfx;
        }

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
}

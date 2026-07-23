using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Applies a session's presentation policy to a spawned world object: <see cref="IPresentationPart"/>
    /// behaviours toggle themselves, then every renderer, particle system, and audio source under the
    /// root is switched generically — new presentation hardware on a prefab is covered without opting in.
    /// Pooled instances cross sessions, so owning seams re-apply per checkout (never once per Awake)
    /// and cache the captured parts per instance.
    /// </summary>
    public static class PresentationApplier
    {
        public readonly struct Parts
        {
            public readonly IPresentationPart[] Behaviours;
            public readonly Renderer[] Renderers;
            public readonly ParticleSystem[] Particles;
            public readonly AudioSource[] AudioSources;

            public Parts(GameObject root)
            {
                Behaviours = root.GetComponentsInChildren<IPresentationPart>(true);
                Renderers = root.GetComponentsInChildren<Renderer>(true);
                Particles = root.GetComponentsInChildren<ParticleSystem>(true);
                AudioSources = root.GetComponentsInChildren<AudioSource>(true);
            }
        }

        public static Parts Capture(GameObject root) => new(root);

        public static void Apply(in Parts parts, bool visible)
        {
            foreach (var part in parts.Behaviours)
                part.ApplyPresentation(visible);

            foreach (var renderer in parts.Renderers)
                renderer.enabled = visible;

            foreach (var ps in parts.Particles)
            {
                if (visible)
                {
                    // Restart only what activation would have started itself — burst systems stay armed.
                    if (ps.main.playOnAwake && !ps.isPlaying) ps.Play(true);
                }
                else
                {
                    // A disabled renderer does not stop simulation; clear live particles too.
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            foreach (var source in parts.AudioSources)
            {
                if (!visible) source.Stop();
                source.enabled = visible;
            }
        }

        public static void Apply(GameObject root, bool visible) => Apply(Capture(root), visible);
    }
}

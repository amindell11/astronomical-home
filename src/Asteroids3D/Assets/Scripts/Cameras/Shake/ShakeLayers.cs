using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cameras.Shake
{
    public sealed class ShakeLayers
    {
        private readonly List<Layer> layers = new();
        private int issued;

        public int Count => layers.Count;

        public void Start(Object source, CameraShakeProfile profile, Vector2 direction, float now)
        {
            var layer = Find(source);
            if (layer == null)
            {
                layer = new Layer();
                layers.Add(layer);
            }

            layer.source = source;
            layer.profile = profile;
            layer.direction = direction.normalized;
            layer.start = now;
            layer.released = false;
            // Noise channels 0 to 2 belong to ObserverCam's trauma shake.
            layer.channel = issued * 3 + 3;
            issued++;
        }

        public void Release(Object source, float now)
        {
            var layer = Find(source);
            if (layer == null || layer.released) return;
            layer.released = true;
            layer.releaseAt = now;
        }

        public void Clear() => layers.Clear();

        public void Sample(float now, float halfHeight, out Vector2 offset, out float roll)
        {
            offset = Vector2.zero;
            roll = 0f;
            for (var i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                var t = layer.EnvelopeTime(now);
                if (t > layer.profile.Length && (layer.profile.sustainAt < 0f || layer.released))
                {
                    layers.RemoveAt(i);
                    continue;
                }

                var e = Mathf.Clamp01(layer.profile.envelope.Evaluate(t));
                var frequency = layer.profile.frequency;
                var channel = layer.channel;
                offset += new Vector2(CameraUtils.ShakeNoise(channel, now, frequency),
                    CameraUtils.ShakeNoise(channel + 1, now, frequency)) * (e * layer.profile.jitter * halfHeight);
                offset += layer.direction * (e * layer.profile.kick * halfHeight);
                roll += CameraUtils.ShakeNoise(channel + 2, now, frequency) * e * layer.profile.roll;
            }
        }

        private Layer Find(Object source)
        {
            foreach (var layer in layers)
                if (ReferenceEquals(layer.source, source))
                    return layer;
            return null;
        }

        private sealed class Layer
        {
            public Object source;
            public CameraShakeProfile profile;
            public Vector2 direction;
            public float start;
            public bool released;
            public float releaseAt;
            public int channel;

            public float EnvelopeTime(float now)
            {
                var elapsed = now - start;
                if (profile.sustainAt < 0f)
                    return elapsed;
                if (!released)
                    return Mathf.Min(elapsed, profile.sustainAt);
                return Mathf.Min(releaseAt - start, profile.sustainAt) + (now - releaseAt);
            }
        }
    }
}

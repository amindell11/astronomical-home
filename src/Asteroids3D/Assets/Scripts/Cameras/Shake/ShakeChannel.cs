using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cameras.Shake
{
    /// <summary>
    /// A ship's outlet for its own shakes. Parts on the ship raise and release, and the flight camera
    /// listens only to its subject's channel.
    /// </summary>
    public sealed class ShakeChannel
    {
        public event Action<Object, CameraShakeProfile, Vector2> Raised;
        public event Action<Object> Released;

        public void Raise(Object source, CameraShakeProfile profile, Vector2 direction)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (!profile) throw new ArgumentNullException(nameof(profile));
            Raised?.Invoke(source, profile, direction);
        }

        public void Release(Object source) => Released?.Invoke(source);
    }
}

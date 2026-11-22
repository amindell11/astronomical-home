using System;
using UnityEngine;

namespace Cameras.Shake
{
    /// <summary>
    /// The one flight camera that hears nearby blasts. Shake emitters on blast prefabs announce here, and the registered
    /// camera turns each into a shake layer. Interim: a process-wide registry, acceptable only because shake is
    /// presentation, which multi-arena sessions run without; not a pattern for gameplay state.
    /// </summary>
    public static class ShakeListener
    {
        private static IShakeListener current;

        public static void Register(IShakeListener listener)
        {
            if (current != null && !ReferenceEquals(current, listener))
                throw new InvalidOperationException("A shake listener is already registered.");
            current = listener;
        }

        public static void Unregister(IShakeListener listener)
        {
            if (ReferenceEquals(current, listener))
                current = null;
        }

        public static void Hear(Vector3 position, CameraShakeProfile profile, float size = 0f)
        {
            if (!profile)
                throw new ArgumentNullException(nameof(profile));
            if (current == null)
                return;
            current.Hear(position, profile, size);
        }
    }
}

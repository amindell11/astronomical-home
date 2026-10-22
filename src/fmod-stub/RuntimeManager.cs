using UnityEngine;

namespace FMODUnity
{
    public static class RuntimeManager
    {
        public static FMOD.Studio.EventInstance CreateInstance(EventReference eventReference) => default;

        public static void AttachInstanceToGameObject(FMOD.Studio.EventInstance instance, GameObject gameObject, bool nonRigidbodyVelocity = false) { }

        public static void AttachInstanceToGameObject(FMOD.Studio.EventInstance instance, GameObject gameObject, Rigidbody rigidBody) { }

        public static void DetachInstanceFromGameObject(FMOD.Studio.EventInstance instance) { }

        public static void PlayOneShot(EventReference eventReference, Vector3 position = new Vector3()) { }

        public static void PlayOneShotAttached(EventReference eventReference, GameObject gameObject) { }
    }
}

using UnityEngine;

namespace FMODUnity
{
    public static class RuntimeUtils
    {
        public static FMOD.ATTRIBUTES_3D To3DAttributes(this Vector3 pos) => default;

        public static FMOD.ATTRIBUTES_3D To3DAttributes(Transform transform, Rigidbody rigidbody = null) => default;
    }
}

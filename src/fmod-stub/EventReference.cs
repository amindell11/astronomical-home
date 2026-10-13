using System;

namespace FMODUnity
{
    [Serializable]
    public struct EventReference
    {
        public FMOD.GUID Guid;

#if UNITY_EDITOR
        public string Path;
#endif
    }
}

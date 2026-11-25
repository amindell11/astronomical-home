using System;
using UnityEngine;

namespace FMODUnity
{
    public class PlatformCallbackHandler : ScriptableObject
    {
        public virtual void PreInitialize(FMOD.Studio.System system, Action<FMOD.RESULT, string> reportResult)
        {
        }
    }
}

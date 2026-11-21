using System;
using UnityEngine;

namespace UI.Hangar.Hardware
{
    [Serializable]
    public struct HangarGlow
    {
        public MeshRenderer card;
        public Color color;
        public Vector2 size;
    }
}

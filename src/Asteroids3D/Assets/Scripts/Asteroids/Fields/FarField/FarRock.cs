using System.Runtime.InteropServices;
using UnityEngine;

namespace Asteroids.Fields.FarField
{
    /// <summary>One billboard instance; the layout mirrors <c>FarRock</c> in FarFieldRock.shader.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FarRock
    {
        public const int Stride = 28;

        public Vector3 Position;
        public float HalfSize;
        public float Spin;
        public float Cell;
        public float Alpha;
    }
}

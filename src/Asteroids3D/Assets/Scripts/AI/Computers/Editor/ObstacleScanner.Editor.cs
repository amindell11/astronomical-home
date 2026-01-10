#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace AI.Computers
{
    public partial class ObstacleScanner
    {
        public List<Vector3> DebugRays { get; } = new List<Vector3>();

        partial void ClearDebugRays() => DebugRays.Clear();
        partial void AddDebugRay(Vector3 ray) => DebugRays.Add(ray);
    }
}
#endif

using System;
using UnityEngine;

namespace Utils
{
    public static class SuperGizmos 
    {
        public enum HeadType {
            Sphere,
            Cube,
        }
        public static void DrawArrow(Vector3 position, Vector3 vector, HeadType headType = HeadType.Sphere, float headSize = 0.1f, Color color = default, float scale = 1f, float headScale = 1f)  
        {
            if (vector == Vector3.zero) return;
            if (color != default)
            {
                Gizmos.color = color;
            }
            Gizmos.DrawRay(position, vector * scale);
            switch (headType)
            {
                case HeadType.Sphere:
                    Gizmos.DrawWireSphere(position + vector*scale, headSize * headScale);
                    break;
                case HeadType.Cube:
                    Gizmos.DrawWireCube(position + vector*scale, Vector3.one * headSize * headScale);
                    break;
                default:
                    Gizmos.DrawWireSphere(position + vector*scale, headSize * headScale);
                    break;
            }
        }

        public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, int segments = 20)
        {
            if (Mathf.Abs(angle) < 0.01f) return;
            
            var fromDir = from.normalized;
            var binormal = Vector3.Cross(normal, fromDir);
            
            var prevPoint = center + fromDir * radius;
            for (var i = 1; i <= segments; i++)
            {
                var t = (i / (float)segments) * angle * Mathf.Deg2Rad;
                var point = center + (fromDir * Mathf.Cos(t) + binormal * Mathf.Sin(t)) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
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
            
            var n = normal.normalized;
            var f = from.normalized;
            
            var binormal = Vector3.Cross(n, f);
            
            if (binormal.sqrMagnitude < 0.0001f)
            {
                // 'from' is parallel to 'normal'. Use an arbitrary perpendicular vector.
                var fallback = Mathf.Abs(Vector3.Dot(n, Vector3.up)) < 0.9f ? Vector3.up : Vector3.forward;
                binormal = Vector3.Cross(n, fallback).normalized;
            }
            else
            {
                binormal.Normalize();
            }

            // Recalculate 'from' to be perfectly perpendicular to 'normal'
            var realFrom = Vector3.Cross(binormal, n).normalized;
            
            var prevPoint = center + realFrom * radius;
            for (var i = 1; i <= segments; i++)
            {
                var t = (i / (float)segments) * angle * Mathf.Deg2Rad;
                var point = center + (realFrom * Mathf.Cos(t) + binormal * Mathf.Sin(t)) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
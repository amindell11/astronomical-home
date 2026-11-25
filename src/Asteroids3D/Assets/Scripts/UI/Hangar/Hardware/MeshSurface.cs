using UnityEngine;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// Triangles of one console mesh. The glass is curved, so a hit is a real triangle, not a plane.
    /// </summary>
    public sealed class MeshSurface
    {
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly int[] triangles;

        public MeshSurface(Mesh mesh)
        {
            vertices = mesh.vertices;
            uvs = mesh.uv;
            triangles = mesh.triangles;
        }

        public bool Raycast(Ray worldRay, Transform transform, out float distance, out Vector2 uv)
        {
            var origin = transform.InverseTransformPoint(worldRay.origin);
            var direction = transform.InverseTransformVector(worldRay.direction);
            distance = float.PositiveInfinity;
            uv = default;
            var hit = false;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (!Intersect(origin, direction, vertices[a], vertices[b], vertices[c], out var t, out var bary))
                    continue;
                if (t < 0f || t >= distance) continue;
                distance = t;
                uv = uvs[a] * bary.x + uvs[b] * bary.y + uvs[c] * bary.z;
                hit = true;
            }
            return hit;
        }

        public Vector3 LocalPoint(Vector2 uv)
        {
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (!Contains(uv, uvs[a], uvs[b], uvs[c], out var bary)) continue;
                return vertices[a] * bary.x + vertices[b] * bary.y + vertices[c] * bary.z;
            }
            throw new System.InvalidOperationException($"No triangle covers UV {uv}.");
        }

        private static bool Intersect(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
            out float distance, out Vector3 bary)
        {
            distance = 0f;
            bary = default;
            var edge1 = b - a;
            var edge2 = c - a;
            var p = Vector3.Cross(direction, edge2);
            var det = Vector3.Dot(edge1, p);
            if (det > -1e-8f && det < 1e-8f) return false;
            var inv = 1f / det;
            var tvec = origin - a;
            var u = Vector3.Dot(tvec, p) * inv;
            if (u < -1e-5f || u > 1f + 1e-5f) return false;
            var q = Vector3.Cross(tvec, edge1);
            var v = Vector3.Dot(direction, q) * inv;
            if (v < -1e-5f || u + v > 1f + 1e-5f) return false;
            distance = Vector3.Dot(edge2, q) * inv;
            if (distance < 0f) return false;
            bary = new Vector3(1f - u - v, u, v);
            return true;
        }

        private static bool Contains(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 bary)
        {
            bary = default;
            var edge1 = b - a;
            var edge2 = c - a;
            var den = edge1.x * edge2.y - edge2.x * edge1.y;
            if (den > -1e-8f && den < 1e-8f) return false;
            var diff = point - a;
            var v = (diff.x * edge2.y - edge2.x * diff.y) / den;
            var w = (edge1.x * diff.y - diff.x * edge1.y) / den;
            var u = 1f - v - w;
            if (u < -1e-4f || v < -1e-4f || w < -1e-4f) return false;
            bary = new Vector3(u, v, w);
            return true;
        }
    }
}

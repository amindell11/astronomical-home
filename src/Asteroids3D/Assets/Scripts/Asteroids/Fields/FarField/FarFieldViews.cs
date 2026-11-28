using UnityEngine;

namespace Asteroids.Fields.FarField
{
    public readonly struct FarFieldPick
    {
        public readonly int View;
        public readonly float Spin;

        public FarFieldPick(int view, float spin)
        {
            View = view;
            Spin = spin;
        }
    }

    /// <summary>
    /// The atlas's fixed view directions in mesh-local space (the 12 icosahedron vertices), each with
    /// the bake camera's up/right, and the pose split shared by the bake and the runtime layer: a
    /// rock's rotation becomes the nearest baked view plus a spin about the camera axis. Spin is
    /// counter-clockwise in the screen's right/up frame, radians.
    /// </summary>
    public static class FarFieldViews
    {
        public const int Count = 12;

        private static readonly Vector3[] directions = BuildDirections();
        private static readonly Vector3[] ups = BuildUps(directions);
        private static readonly Vector3[] rights = BuildRights(directions, ups);

        public static Vector3 Direction(int view) => directions[view];
        public static Vector3 Up(int view) => ups[view];
        public static Vector3 Right(int view) => rights[view];

        public static FarFieldPick Pick(Quaternion rotation, Vector3 toCamera, Vector3 screenRight, Vector3 screenUp)
        {
            var local = Quaternion.Inverse(rotation) * toCamera;
            var view = 0;
            var best = float.NegativeInfinity;
            for (var k = 0; k < Count; k++)
            {
                var d = Vector3.Dot(local, directions[k]);
                if (d <= best) continue;
                best = d;
                view = k;
            }
            var w = rotation * ups[view];
            var spin = Mathf.Atan2(-Vector3.Dot(w, screenRight), Vector3.Dot(w, screenUp));
            return new FarFieldPick(view, spin);
        }

        private static Vector3[] BuildDirections()
        {
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var raw = new[]
            {
                new Vector3(0, 1, phi), new Vector3(0, 1, -phi), new Vector3(0, -1, phi), new Vector3(0, -1, -phi),
                new Vector3(1, phi, 0), new Vector3(1, -phi, 0), new Vector3(-1, phi, 0), new Vector3(-1, -phi, 0),
                new Vector3(phi, 0, 1), new Vector3(phi, 0, -1), new Vector3(-phi, 0, 1), new Vector3(-phi, 0, -1)
            };
            for (var i = 0; i < raw.Length; i++) raw[i] = raw[i].normalized;
            return raw;
        }

        private static Vector3[] BuildUps(Vector3[] dirs)
        {
            var result = new Vector3[dirs.Length];
            for (var i = 0; i < dirs.Length; i++)
                result[i] = Vector3.ProjectOnPlane(Vector3.up, dirs[i]).normalized;
            return result;
        }

        private static Vector3[] BuildRights(Vector3[] dirs, Vector3[] upVectors)
        {
            var result = new Vector3[dirs.Length];
            for (var i = 0; i < dirs.Length; i++)
                result[i] = Vector3.Cross(upVectors[i], -dirs[i]);
            return result;
        }
    }
}

using UnityEngine;
using Utils;

namespace Game
{
    /// <summary>
    /// Centralised utility for converting between world-space and the game's abstract 2-D plane.
    /// The plane is defined by a single <see cref="Transform"/> (usually tagged
    /// "ReferencePlane") whose position is the origin, <c>up</c> is +Y on the plane,
    /// and <c>forward</c> is the plane normal.
    /// </summary>
    public static class GamePlane
    {
        private static Transform _plane;

        /// <summary>Assigns the reference plane explicitly (e.g., from a bootstrap script).</summary>
        public static void SetReferencePlane(Transform t) => _plane = t;
    
        /// <summary>Returns the cached plane or attempts to find a GameObject tagged "ReferencePlane".</summary>
        public static Transform Plane => _plane ?? CachePlane();
    
        private static Transform CachePlane()
        {
            var go = GameObject.FindGameObjectWithTag(TagNames.ReferencePlane);
            _plane = go?.transform ?? CreateReferencePlane();
            return _plane;
        }

        private static Transform CreateReferencePlane()
        {
            var go = new GameObject(TagNames.ReferencePlane);
            go.tag = TagNames.ReferencePlane;
            return go.transform;
        }
        public static Vector3 Origin  => Plane ? Plane.position : Vector3.zero;
        public static Vector3 Normal  => Plane ? Plane.forward : Vector3.forward;
        public static Vector3 Forward => Plane ? Plane.up       : Vector3.up;
        public static Vector3 Right   => Plane ? Plane.right    : Vector3.right;

        /// <summary>Projects a world-space vector onto the plane basis and returns XY components.</summary>
        public static Vector2 WorldToPlane(Vector3 world)
        {
            var offset = world - Origin;
            return new Vector2(Vector3.Dot(offset, Right), Vector3.Dot(offset, Forward));
        }

        /// <summary>Converts plane-space coordinates back to world-space.</summary>
        public static Vector3 PlaneToWorld(Vector2 plane) => Origin + Right * plane.x + Forward * plane.y;

        /// <summary>Returns the component of <paramref name="world"/> lying in the plane (world-space coords).</summary>
        public static Vector3 ProjectOntoPlane(Vector3 world) => Vector3.ProjectOnPlane(world - Origin, Normal);

        /// <summary>Converts a plane-space vector into a world-space direction.</summary>
        public static Vector3 PlaneVectorToWorld(Vector2 v) => Right * v.x + Forward * v.y;
    }
} 
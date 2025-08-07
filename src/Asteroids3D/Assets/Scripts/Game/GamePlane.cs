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
        public static Vector3 Origin  => Plane.position;
        public static Vector3 Normal  => Plane.forward;
        public static Vector3 Forward => Plane.up;
        public static Vector3 Right   => Plane.right;

        public static Vector3 ProjectOntoPlane(Vector3 world) => 
            Vector3.ProjectOnPlane(world - Origin, Normal);

        public static Vector2 WorldPointToPlane(Vector3 worldPt) => 
            (Vector2)Plane.InverseTransformPoint(worldPt);
        public static Vector2 WorldDirToPlane(Vector3 worldDir) => 
            (Vector2)Plane.InverseTransformDirection(worldDir);
        public static Vector3 PlanePointToWorld(Vector2 planePt) => 
            Plane.TransformPoint(new Vector3(planePt.x, planePt.y, 0f));
        public static Vector3 PlaneDirToWorld(Vector2 planeDir) => 
            Plane.TransformDirection(new Vector3(planeDir.x, planeDir.y, 0f));
    }
} 
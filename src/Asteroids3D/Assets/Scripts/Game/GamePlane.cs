using UnityEngine;

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
        public static void SetReferencePlane(Transform t)
        {
            if (!t)
                throw new System.ArgumentNullException(nameof(t), "GamePlane reference cannot be null.");

            if (_plane && _plane != t)
                throw new System.InvalidOperationException("GamePlane has already been set. Reset before binding a different reference.");

            _plane = t;
        }

        /// <summary>Clears the cached reference plane. Useful for test teardown.</summary>
        public static void Reset() => _plane = null;

        /// <summary>Returns the configured reference plane. Throws if not configured.</summary>
        public static Transform Plane => _plane
            ? _plane
            : throw new System.InvalidOperationException("GamePlane not configured. Call GamePlane.SetReferencePlane() during bootstrap.");
        public static Vector3 Origin  => Plane.position;
        public static Vector3 Normal  => Plane.forward;
        public static Vector3 Forward => Plane.up;
        public static Vector3 Right   => Plane.right;

        public static Vector3 ProjectOntoPlane(Vector3 world) => 
            Vector3.ProjectOnPlane(world - Origin, Normal);

        public static Vector2 WorldPointToPlane(Vector3 worldPt)
        {
            var relative = worldPt - Origin;
            var projected = Vector3.ProjectOnPlane(relative, Normal);
            return new Vector2(Vector3.Dot(projected, Right), Vector3.Dot(projected, Forward));
        }
        
        public static Vector2 WorldDirToPlane(Vector3 worldDir)
        {
            var projected = Vector3.ProjectOnPlane(worldDir, Normal);
            return new Vector2(Vector3.Dot(projected, Right), Vector3.Dot(projected, Forward));
        }
        
        public static Vector3 PlanePointToWorld(Vector2 planePt) => 
            Origin + Right * planePt.x + Forward * planePt.y;
        
        public static Vector3 PlaneDirToWorld(Vector2 planeDir) => 
            Right * planeDir.x + Forward * planeDir.y;
    }

    public static class PlaneConstraints
    {
        public static void ConstrainPosition(Transform target)
        {
            if (!target) return;
            target.position = GamePlane.ProjectOntoPlane(target.position) + GamePlane.Origin;
        }

        public static void ConstrainPositionAndVelocity(Transform target, Rigidbody body)
        {
            ConstrainPosition(target);
            if (!body) return;
            body.linearVelocity = Vector3.ProjectOnPlane(body.linearVelocity, GamePlane.Normal);
        }

        public static void AlignTransformUpToPlane(Transform target)
        {
            if (!target) return;

            var projectedUp = Vector3.ProjectOnPlane(target.up, GamePlane.Normal);
            if (projectedUp.sqrMagnitude < 1e-6f)
            {
                target.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
                return;
            }

            projectedUp.Normalize();
            var toPlane = Quaternion.FromToRotation(target.up, projectedUp);
            target.rotation = toPlane * target.rotation;
        }
    }
}

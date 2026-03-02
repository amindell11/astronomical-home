using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Immutable snapshot of the current gameplay plane frame.
    /// </summary>
    public readonly struct PlaneFrame
    {
        public Vector3 Origin { get; }
        public Vector3 Normal { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public Quaternion Rotation { get; }

        public PlaneFrame(Transform referencePlane)
        {
            if (!referencePlane)
                throw new ArgumentNullException(nameof(referencePlane), "Reference plane transform cannot be null.");

            Origin = referencePlane.position;
            Normal = referencePlane.forward;
            Forward = referencePlane.up;
            Right = referencePlane.right;
            Rotation = referencePlane.rotation;
        }

        public Vector2 ToPlanePoint(Vector3 worldPoint)
        {
            var relative = worldPoint - Origin;
            return new Vector2(Vector3.Dot(relative, Right), Vector3.Dot(relative, Forward));
        }

        public Vector2 ToPlaneVector(Vector3 worldVector)
        {
            var projected = Vector3.ProjectOnPlane(worldVector, Normal);
            return new Vector2(Vector3.Dot(projected, Right), Vector3.Dot(projected, Forward));
        }

        public Vector3 ToWorldPoint(Vector2 planePoint) =>
            Origin + Right * planePoint.x + Forward * planePoint.y;

        public Vector3 ToWorldVector(Vector2 planeVector) =>
            Right * planeVector.x + Forward * planeVector.y;

        /// <summary>
        /// Projects a world-space point onto the gameplay plane and returns a world-space point.
        /// </summary>
        public Vector3 ProjectWorldPointToPlaneWorld(Vector3 worldPoint)
        {
            var signedDistance = Vector3.Dot(worldPoint - Origin, Normal);
            return worldPoint - Normal * signedDistance;
        }

        /// <summary>
        /// Projects a world-space vector onto the gameplay plane and returns a world-space vector.
        /// </summary>
        public Vector3 ProjectWorldVectorToPlaneWorld(Vector3 worldVector) =>
            Vector3.ProjectOnPlane(worldVector, Normal);
    }

    public interface IGamePlane
    {
        PlaneFrame CurrentFrame { get; }
    }

    /// <summary>
    /// Centralized utility for converting between world-space and the game's abstract 2-D plane.
    ///
    /// Setup is explicit and required: <see cref="SetReferencePlane"/> must be called during
    /// bootstrap. Missing plane configuration is treated as a fatal setup error.
    /// </summary>
    public static class GamePlane
    {
        private static Transform _referencePlane;

        public static bool IsConfigured => _referencePlane;

        /// <summary>Assigns the reference plane explicitly (bootstrap / test setup).</summary>
        public static void SetReferencePlane(Transform plane)
        {
            if (!plane)
                throw new ArgumentNullException(nameof(plane), "GamePlane requires a non-null reference plane.");

            _referencePlane = plane;
        }

        /// <summary>Clears the cached reference plane. Useful for test teardown.</summary>
        public static void Reset() => _referencePlane = null;

        /// <summary>Returns the configured reference plane. Throws when unconfigured.</summary>
        public static Transform Plane => _referencePlane ? _referencePlane : throw NotConfiguredException();

        public static PlaneFrame Frame => new(Plane);

        public static Vector3 Origin => Frame.Origin;
        public static Vector3 Normal => Frame.Normal;
        public static Vector3 Forward => Frame.Forward;
        public static Vector3 Right => Frame.Right;

        public static Vector3 ProjectWorldPointToPlaneWorld(Vector3 worldPoint) =>
            Frame.ProjectWorldPointToPlaneWorld(worldPoint);

        public static Vector2 WorldPointToPlane(Vector3 worldPoint) =>
            Frame.ToPlanePoint(worldPoint);

        public static Vector2 WorldDirToPlane(Vector3 worldDirection) =>
            Frame.ToPlaneVector(worldDirection);

        public static Vector3 PlanePointToWorld(Vector2 planePoint) =>
            Frame.ToWorldPoint(planePoint);

        public static Vector3 PlaneDirToWorld(Vector2 planeDirection) =>
            Frame.ToWorldVector(planeDirection);

        private static InvalidOperationException NotConfiguredException() =>
            new("GamePlane is not configured. Call GamePlane.SetReferencePlane(...) during world bootstrap before use.");
    }
}

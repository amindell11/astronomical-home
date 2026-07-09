using UnityEngine;

namespace Game
{
    /// <summary>
    /// Which world axis serves as the game-plane normal (the "off-plane" direction).
    /// </summary>
    public enum PlaneAxis { X, Y, Z }

    /// <summary>
    /// Centralised utility for converting between world-space and the game's abstract 2-D plane.
    /// Configured once at startup with a <see cref="PlaneAxis"/> and an origin position.
    /// </summary>
    public static class GamePlane
    {
        private static bool _configured;
        private static Vector3 _origin;
        private static Vector3 _normal;
        private static Vector3 _forward;
        private static Vector3 _right;
        private static RigidbodyConstraints _positionConstraint;
        private static Quaternion _rotation;

        /// <summary>Configure the game plane from an axis enum and world-space origin.</summary>
        public static void Configure(PlaneAxis axis, Vector3 origin = default)
        {
            if (_configured)
                throw new System.InvalidOperationException(
                    "GamePlane has already been configured. Call Reset() before reconfiguring.");

            _origin = origin;

            switch (axis)
            {
                case PlaneAxis.X:
                    _normal  = Vector3.right;
                    _forward = Vector3.forward;
                    _right   = Vector3.up;
                    _positionConstraint = RigidbodyConstraints.FreezePositionX;
                    break;
                case PlaneAxis.Y:
                    _normal  = Vector3.down;
                    _forward = Vector3.forward;
                    _right   = Vector3.right;
                    _positionConstraint = RigidbodyConstraints.FreezePositionY;
                    break;
                case PlaneAxis.Z:
                    _normal  = Vector3.forward;
                    _forward = Vector3.up;
                    _right   = Vector3.right;
                    _positionConstraint = RigidbodyConstraints.FreezePositionZ;
                    break;
            }

            _rotation = PlanePose(_normal, _forward);
            _configured = true;
        }

        /// <summary>
        /// The plane-dweller pose convention, pure (no configured-state dependency): forward = plane
        /// normal (points AWAY from the top-down camera; the visible hull face is -forward),
        /// up = in-plane heading (nose).
        /// </summary>
        public static Quaternion PlanePose(Vector3 planeNormal, Vector3 heading) =>
            Quaternion.LookRotation(planeNormal, heading);

        /// <summary>Clears the configuration. Useful for test teardown and restarts.</summary>
        public static void Reset()
        {
            _configured = false;
            _origin = Vector3.zero;
        }

        /// <summary>Returns true if the game plane has been configured.</summary>
        public static bool IsConfigured => _configured;

        public static Vector3 Origin  { get { AssertConfigured(); return _origin; } }
        public static Vector3 Normal  { get { AssertConfigured(); return _normal; } }
        public static Vector3 Forward { get { AssertConfigured(); return _forward; } }
        public static Vector3 Right   { get { AssertConfigured(); return _right; } }

        /// <summary>Base rotation for objects lying in the game plane (forward along normal, up along plane forward).</summary>
        public static Quaternion Rotation { get { AssertConfigured(); return _rotation; } }

        /// <summary>The RigidbodyConstraints flag that freezes movement along the off-plane axis.</summary>
        public static RigidbodyConstraints PositionConstraint
        {
            get { AssertConfigured(); return _positionConstraint; }
        }

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

        private static void AssertConfigured()
        {
            if (!_configured)
                throw new System.InvalidOperationException(
                    "GamePlane not configured. Call GamePlane.Configure() during bootstrap.");
        }
    }

    public static class PlaneConstraints
    {
        /// <summary>
        /// Applies the appropriate RigidbodyConstraints to freeze the off-plane position axis.
        /// Call once at startup, not every frame.
        /// </summary>
        public static void ConstrainBodyToPlane(Rigidbody body)
        {
            if (!body) return;
            body.constraints |= GamePlane.PositionConstraint;
        }

        /// <summary>Teleports a non-physics transform onto the game plane. For Rigidbodies, use ConstrainBodyToPlane instead.</summary>
        public static void ConstrainPosition(Transform target)
        {
            if (!target) return;
            target.position = GamePlane.ProjectOntoPlane(target.position) + GamePlane.Origin;
        }

    }
}

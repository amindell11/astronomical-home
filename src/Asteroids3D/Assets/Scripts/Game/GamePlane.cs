using UnityEngine;

namespace Game
{
    public enum PlaneAxis { X, Y, Z }

    /// <summary>
    /// Immutable basis for a game plane: the axis/origin-parameterised conversions between world
    /// space and the abstract 2-D plane. The shipped game uses one frozen instance
    /// (<see cref="GamePlane.Canonical"/>); the parameterised constructor exists for tests.
    /// </summary>
    public readonly struct GamePlaneFrame
    {
        public Vector3 Origin { get; }
        public Vector3 Normal { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public RigidbodyConstraints PositionConstraint { get; }

        /// <summary>Base rotation for objects lying in the plane (forward along normal, up along plane forward).</summary>
        public Quaternion Rotation { get; }

        public GamePlaneFrame(PlaneAxis axis, Vector3 origin = default)
        {
            Origin = origin;

            switch (axis)
            {
                case PlaneAxis.X:
                    Normal  = Vector3.right;
                    Forward = Vector3.forward;
                    Right   = Vector3.up;
                    PositionConstraint = RigidbodyConstraints.FreezePositionX;
                    break;
                case PlaneAxis.Y:
                    Normal  = Vector3.down;
                    Forward = Vector3.forward;
                    Right   = Vector3.right;
                    PositionConstraint = RigidbodyConstraints.FreezePositionY;
                    break;
                default:
                    Normal  = Vector3.forward;
                    Forward = Vector3.up;
                    Right   = Vector3.right;
                    PositionConstraint = RigidbodyConstraints.FreezePositionZ;
                    break;
            }

            Rotation = PlanePose(Normal, Forward);
        }

        /// <summary>Plane-dweller pose: forward = plane normal (points AWAY from the top-down camera), up = in-plane heading.</summary>
        public static Quaternion PlanePose(Vector3 planeNormal, Vector3 heading) =>
            Quaternion.LookRotation(planeNormal, heading);

        public Vector3 ProjectOntoPlane(Vector3 world) =>
            Vector3.ProjectOnPlane(world - Origin, Normal);

        public Vector2 WorldPointToPlane(Vector3 worldPt)
        {
            var projected = Vector3.ProjectOnPlane(worldPt - Origin, Normal);
            return new Vector2(Vector3.Dot(projected, Right), Vector3.Dot(projected, Forward));
        }

        public Vector2 WorldDirToPlane(Vector3 worldDir)
        {
            var projected = Vector3.ProjectOnPlane(worldDir, Normal);
            return new Vector2(Vector3.Dot(projected, Right), Vector3.Dot(projected, Forward));
        }

        public Vector3 PlanePointToWorld(Vector2 planePt) =>
            Origin + Right * planePt.x + Forward * planePt.y;

        public Vector3 PlaneDirToWorld(Vector2 planeDir) =>
            Right * planeDir.x + Forward * planeDir.y;
    }

    /// <summary>
    /// The one frozen world plane: a thin facade over the canonical <see cref="GamePlaneFrame"/>.
    /// A compile-time coordinate convention (closer to <see cref="Mathf"/> than to a service), so it
    /// carries no runtime configuration and no lifecycle to coordinate across arenas.
    /// </summary>
    public static class GamePlane
    {
        public static readonly GamePlaneFrame Canonical = new GamePlaneFrame(PlaneAxis.Z);

        public static Vector3 Origin  => Canonical.Origin;
        public static Vector3 Normal  => Canonical.Normal;
        public static Vector3 Forward => Canonical.Forward;
        public static Vector3 Right   => Canonical.Right;
        public static Quaternion Rotation => Canonical.Rotation;

        /// <summary>The RigidbodyConstraints flag that freezes movement along the off-plane axis.</summary>
        public static RigidbodyConstraints PositionConstraint => Canonical.PositionConstraint;

        public static Quaternion PlanePose(Vector3 planeNormal, Vector3 heading) =>
            GamePlaneFrame.PlanePose(planeNormal, heading);

        public static Vector3 ProjectOntoPlane(Vector3 world) => Canonical.ProjectOntoPlane(world);
        public static Vector2 WorldPointToPlane(Vector3 worldPt) => Canonical.WorldPointToPlane(worldPt);
        public static Vector2 WorldDirToPlane(Vector3 worldDir) => Canonical.WorldDirToPlane(worldDir);
        public static Vector3 PlanePointToWorld(Vector2 planePt) => Canonical.PlanePointToWorld(planePt);
        public static Vector3 PlaneDirToWorld(Vector2 planeDir) => Canonical.PlaneDirToWorld(planeDir);
    }

    public static class PlaneConstraints
    {
        /// <summary>Freeze the off-plane position axis on a body. Call once at startup, not every frame.</summary>
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

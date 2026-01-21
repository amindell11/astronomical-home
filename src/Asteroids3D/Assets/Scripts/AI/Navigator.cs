using AI.Computers;
using AI.Context;
using Game;
using Ships;
using Ships.Control;
using Ships.Movement;
using UnityEngine;

namespace AI
{
    public abstract class Navigator : MonoBehaviour
    {
        public struct Waypoint
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isValid;
        }

        protected Ship ship;
        protected Scout scout;
        protected Waypoint currentWaypoint;
        protected bool facingOverride;
        protected float facingAngle;

        public float arriveRadius = 2f;

        public Waypoint CurrentWaypoint => currentWaypoint;

        public virtual void Initialize(Ship ship, Scout scout)
        {
            this.ship = ship;
            this.scout = scout;
            currentWaypoint = new Waypoint { isValid = false };
        }

        public abstract void GenerateNavCommands(State state, ref Command cmd);

        public void SetNavigationPoint(Vector2 point, bool avoid = false, Vector2? velocity = null)
        {
            currentWaypoint.position = point;
            currentWaypoint.velocity = velocity ?? Vector2.zero;
            currentWaypoint.isValid = true;
            OnSetNavigationPoint(avoid);
        }

        protected virtual void OnSetNavigationPoint(bool avoid) { }

        public void SetNavigationPointWorld(Vector3 worldPos, bool avoid = true, Vector3? velocity = null)
        {
            var planePos = GamePlane.WorldPointToPlane(worldPos);
            var planeVel = velocity.HasValue ? GamePlane.WorldDirToPlane(velocity.Value) : (Vector2?)null;
            SetNavigationPoint(planePos, avoid, planeVel);
        }

        public void ClearNavigationPoint()
        {
            currentWaypoint.isValid = false;
        }

        public void SetFacingOverride(float angle)
        {
            facingOverride = true;
            facingAngle = angle;
        }

        public void SetFacingTarget(Vector2 direction)
        {
            if (!(direction.sqrMagnitude > 0.01f)) return;
            var angle = Vector2.SignedAngle(Vector2.up, direction);
            if (angle < 0f) angle += 360f;
            SetFacingOverride(angle);
        }

        public void ClearFacingOverride()
        {
            facingOverride = false;
        }
    }
}

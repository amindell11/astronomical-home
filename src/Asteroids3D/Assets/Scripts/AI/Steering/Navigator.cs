using System;
using AI.Context;
using Game;
using Ships;
using Ships.Command;
using Movement;
using UnityEngine;

namespace AI
{
    [DefaultExecutionOrder(-60)]
    public abstract class Navigator : MonoBehaviour
    {
        public struct Waypoint
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isValid;
        }

        protected Scanning.Scout scout;
        protected Waypoint currentWaypoint;
        protected bool facingOverride;
        protected float facingAngle;
        protected Dynamics dynamics;
        protected Movement.MPC.GoalMode goalMode;
        protected float goalDesiredRange;
        protected float goalRangeTolerance;

        protected Command currentCommand;
        public Command CurrentCommand => currentCommand;
        
        protected System.Func<State> getState;
        public float arriveRadius = 2f;

        public Waypoint CurrentWaypoint => currentWaypoint;

        public virtual void Initialize(Func<State> stateProvider, Dynamics dynamics, Scanning.Scout scout)
        {
            this.getState = stateProvider;
            this.dynamics = dynamics;
            this.scout = scout;
            currentWaypoint = new Waypoint { isValid = false };
        }

        private void FixedUpdate(){
            if(getState !=null) {
                currentCommand = default;
                GenerateNavCommands(getState(), ref currentCommand);
            }
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

        public void SetGoalMaintainRange(float desiredRange, float rangeTolerance)
        {
            goalMode = Movement.MPC.GoalMode.MaintainRange;
            goalDesiredRange = desiredRange;
            goalRangeTolerance = rangeTolerance;
        }

        public void SetGoalFlee()
        {
            goalMode = Movement.MPC.GoalMode.Flee;
        }

        public void ClearGoalMode()
        {
            goalMode = Movement.MPC.GoalMode.Waypoint;
            goalDesiredRange = 0f;
            goalRangeTolerance = 0f;
        }
    }
}

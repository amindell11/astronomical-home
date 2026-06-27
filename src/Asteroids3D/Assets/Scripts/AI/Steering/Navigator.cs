using System;
using AI.Context;
using AI.States;
using Game;
using Ships;
using Ships.Command;
using Movement;
using UnityEngine;
using State = Ships.Command.State;

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
        protected Vector2? navigationTarget;
        protected bool facingOverride;
        protected float facingAngle;
        protected Dynamics dynamics;
        protected Movement.MPC.GoalMode goalMode;
        protected float goalDesiredRange;
        protected float goalRangeTolerance;
        protected float enemyYaw = float.NaN;
        protected float enemyYawRate;
        protected float projectileSpeed;
        protected Dynamics enemyDynamics;
        protected Movement.MPC.WeightMultipliers weightMultipliers = Movement.MPC.WeightMultipliers.Default;

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

        /// <summary>
        /// Single production entry point for driving the navigator. Applies the whole
        /// <see cref="NavigationIntent"/> in one place, resetting every field each call so
        /// the result depends only on the intent — never on prior state or call order.
        /// An invalid intent (<see cref="NavigationIntent.None"/>) resets the navigator to idle.
        /// The granular Set*/Clear* methods below are the low-level seam this composes
        /// (also used directly by tests); production code should call ApplyIntent instead.
        /// </summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid)
            {
                ResetNavigation();
                return;
            }

            switch (intent.goalMode)
            {
                case Movement.MPC.GoalMode.MaintainRange:
                    SetGoalMaintainRange(intent.desiredRange, intent.rangeTolerance);
                    break;
                case Movement.MPC.GoalMode.Flee:
                    SetGoalFlee();
                    break;
                default:
                    ClearGoalMode();
                    break;
            }

            SetNavigationPoint(intent.goalPosition, true, intent.goalVelocity);

            if (intent.navigationTarget.HasValue)
                SetNavigationTarget(intent.navigationTarget.Value);
            else
                ClearNavigationTarget();

            if (intent.hasEnemy)
                SetEnemyState(intent.enemyYawDeg, intent.enemyYawRateDeg, intent.projectileSpeed,
                    intent.enemyDynamics);
            else
                ClearEnemyState();

            SetWeightMultipliers(intent.weightMultipliers);

            if (intent.obstacleExclusion)
                SetObstacleExclusion(intent.obstacleExclusion);
            else
                ClearObstacleExclusion();
        }

        /// <summary>Resets all navigation overrides to idle. Mirrors a fresh, goal-less navigator.</summary>
        public void ResetNavigation()
        {
            ClearNavigationPoint();
            ClearNavigationTarget();
            ClearGoalMode();
            ClearEnemyState();
            ClearObstacleExclusion();
            ClearWeightMultipliers();
        }

        // ── Low-level control surface ──
        // Composed by ApplyIntent; also driven directly by play-mode tests.

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

        /// <summary>
        /// Set a high-level routing override. When set, the MPC's position + heading costs
        /// pull toward this point instead of the goal (currentWaypoint). Range/Flee/tactical
        /// costs continue to use the goal.
        /// </summary>
        public void SetNavigationTarget(Vector2 plane)
        {
            navigationTarget = plane;
        }

        public void SetNavigationTargetWorld(Vector3 worldPos)
        {
            navigationTarget = GamePlane.WorldPointToPlane(worldPos);
        }

        public void ClearNavigationTarget()
        {
            navigationTarget = null;
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

        public void SetEnemyState(float yawDegrees, float yawRateDegrees, float projectileSpeed,
            Dynamics enemyDynamics = default)
        {
            enemyYaw = yawDegrees * Mathf.Deg2Rad;
            enemyYawRate = yawRateDegrees * Mathf.Deg2Rad;
            this.projectileSpeed = projectileSpeed;
            this.enemyDynamics = enemyDynamics;
        }

        public void ClearEnemyState()
        {
            enemyYaw = float.NaN;
            enemyYawRate = 0f;
            projectileSpeed = 0f;
            enemyDynamics = default;
        }

        public void SetWeightMultipliers(Movement.MPC.WeightMultipliers multipliers)
        {
            weightMultipliers = multipliers;
        }

        public void ClearWeightMultipliers()
        {
            weightMultipliers = Movement.MPC.WeightMultipliers.Default;
        }

        public void SetObstacleExclusion(Transform root)
        {
            scout.SetObstacleExclusion(root);
        }

        public void ClearObstacleExclusion()
        {
            scout.ClearObstacleExclusion();
        }
    }
}

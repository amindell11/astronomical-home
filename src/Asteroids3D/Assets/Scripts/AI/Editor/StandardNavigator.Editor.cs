#if UNITY_EDITOR
using AI;
using AI.Debug;
using Movement.Standard;
using Game;
using UnityEngine;

namespace Movement.Standard
{
    public partial class StandardNavigator
    {
        private PathPlanner.DebugInfo dbgPath;
        private Pilot.Output dbgPilot;
        private Vector2 dbgGoal2D;
        private float dbgThrust, dbgStrafe;

        private AICommander cachedCommander;
        private AIDebugSettings CachedSettings
        {
            get
            {
                if (!cachedCommander)
                    cachedCommander = GetComponent<AICommander>();
                return cachedCommander ? cachedCommander.DebugSettings : null;
            }
        }

        partial void StoreDebugState(Vector2 goal, PathPlanner.DebugInfo path, Pilot.Output pilot)
        {
            dbgGoal2D = goal;
            dbgPath = path;
            dbgPilot = pilot;
        }
        partial void StoreDebugState(float thrust, float strafe)
        {
            dbgThrust = thrust;
            dbgStrafe = strafe;
        }

        void OnDrawGizmos() => DrawGizmosImpl(false);
        void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Steering)) return;
            if (!Application.isPlaying || !scout) return;

            // Ship future position
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            var fut3 = GamePlane.PlanePointToWorld(dbgPath.future);
            Gizmos.DrawLine(transform.position, fut3);
            Gizmos.DrawSphere(fut3, 0.3f);

            // Desired velocity vector
            Gizmos.color = Color.green;
            var dvec = GamePlane.PlaneDirToWorld(dbgPath.desired);
            Gizmos.DrawLine(transform.position, transform.position + dvec);

            // Avoidance vector
            Gizmos.color = Color.red;
            var av = GamePlane.PlaneDirToWorld(dbgPath.avoid);
            Gizmos.DrawLine(transform.position, transform.position + av);

            // Resulting acceleration vector
            Gizmos.color = new Color(1f, 0f, 1f);
            var ac = GamePlane.PlaneDirToWorld(dbgPath.accel);
            Gizmos.DrawLine(transform.position, transform.position + ac);

            // Waypoint marker
            Gizmos.color = Color.yellow;
            var goal3 = GamePlane.PlanePointToWorld(dbgGoal2D);
            Gizmos.DrawLine(transform.position, goal3);
            Gizmos.DrawSphere(goal3, 0.4f);
        }
    }
}
#endif

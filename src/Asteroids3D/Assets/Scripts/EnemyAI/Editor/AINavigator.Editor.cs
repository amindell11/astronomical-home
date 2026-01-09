#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace EnemyAI
{
    public partial class AINavigator
    {
        void OnDrawGizmos()
        {
            // Visualise planner internals when selected in the editor
            if (!Application.isPlaying) return;

            // Ship future position
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // cyan
            Vector3 fut3 = GamePlane.PlanePointToWorld(dbgPath.future);
            Gizmos.DrawLine(transform.position, fut3);
            Gizmos.DrawSphere(fut3, 0.3f);

            // Desired velocity vectorR
            Gizmos.color = Color.green;
            Vector3 dvec = GamePlane.PlaneDirToWorld(dbgPath.desired);
            Gizmos.DrawLine(transform.position, transform.position + dvec);

            // Avoidance vector
            Gizmos.color = Color.red;
            Vector3 av = GamePlane.PlaneDirToWorld(dbgPath.avoid);
            Gizmos.DrawLine(transform.position, transform.position + av);

            // Resulting acceleration vector (magenta)
            Gizmos.color = new Color(1f, 0f, 1f);
            Vector3 ac = GamePlane.PlaneDirToWorld(dbgPath.accel);
            Gizmos.DrawLine(transform.position, transform.position + ac);

            // Waypoint marker (distinct yellow)
            Gizmos.color = Color.yellow;
            Vector3 goal3 = GamePlane.PlanePointToWorld(dbgGoal2D);
            Gizmos.DrawLine(transform.position, goal3);
            Gizmos.DrawSphere(goal3, 0.4f);

            // Detection/avoidance sphere radius visualization
            if (enableAvoidance)
            {
                // Draw raycast fan
                Gizmos.color = new Color(1f, 0.75f, 0f, 0.5f); // orange-ish
                if (dbgRays != null)
                {
                    foreach (var ray in dbgRays)
                    {
                        Gizmos.DrawLine(transform.position, transform.position + ray);
                        if (sphereCastRadius > 0)
                        {
                            Gizmos.DrawWireSphere(transform.position + ray, sphereCastRadius);
                        }
                    }
                }

                // Draw detected asteroids prior to filtering logic
                Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.6f); // greyish
                for (int i = 0; i < dbgHitCount && i < hits.Length; i++)
                {
                    Collider c = hits[i];
                    if (c)
                    {
                        Vector3 p = c.transform.position;
                        float rad = c.bounds.extents.x;
                        Gizmos.DrawWireSphere(p, rad);
                    }
                }
            }
        }
    }
}
#endif

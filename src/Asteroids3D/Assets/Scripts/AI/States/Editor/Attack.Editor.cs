#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Attack
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);

            var position = ctx.ShipInfo.Pos3D;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(position, utilityTuning.attack.facingDistance); // Close range

            var combat = ctx.CombatTracker;
            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);
                var a = ctx.Assessment;

                // Line to enemy - color based on line of sight
                Gizmos.color = a.HasLineOfSight ? Color.red : new Color(1f, 0.5f, 0f, 0.7f);
                Gizmos.DrawLine(position, enemyPos);

                // Enemy marker
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(enemyPos, Vector3.one * 2f);

                // Show targeting crosshair if close
                if (a.EnemyDistance < utilityTuning.attack.facingDistance)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(enemyPos, 1f);

                    // Draw crosshair
                    var cross = Vector3.one * 0.5f;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                    cross.x *= -1;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                }

                // Enemy info label
                UnityEditor.Handles.color = Color.red;
                var enemyInfo = $"TARGET: {combat.Enemy.name}\n{a.EnemyDistance:F1}m";
                if (a.HasLineOfSight) enemyInfo += " (LOS)";
                else enemyInfo += " (No LOS)";
                enemyInfo += $"\nEnemy HP: {combat.EnemyHealthPct:P0}";
                UnityEditor.Handles.Label(enemyPos + Vector3.up, enemyInfo);
            }

            // Show attack state info
            var a2 = ctx.Assessment;
            UnityEditor.Handles.color = Color.white;
            var info = $"ATTACK\nHP: {a2.HealthPct:P0} Shield: {a2.ShieldPct:P0}";
            if (a2.NearbyEnemyCount > a2.NearbyFriendCount)
                info += $"\n⚠ Outnumbered {a2.NearbyEnemyCount}v{a2.NearbyFriendCount}";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }
    }
}
#endif

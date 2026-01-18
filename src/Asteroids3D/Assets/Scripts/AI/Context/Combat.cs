using AI.Computers;
using UnityEngine;

namespace AI.Context
{
    public class Combat
    {
        private readonly Sensors sensors;
        private readonly Gunner gunner;
        private readonly Targeting targeting;

        private Ships.Ship enemyShip;

        public Combat(Sensors sensors, Gunner gunner, Targeting targeting)
        {
            this.sensors = sensors;
            this.gunner = gunner;
            this.targeting = targeting;
        }

        public bool InCombat => enemyShip && enemyShip.gameObject.activeInHierarchy;
        public Ships.Ship Enemy => InCombat ? enemyShip : (enemyShip = sensors.FindNearestEnemy());
        public Vector2 EnemyPos => Enemy?.CurrentState.Kinematics.Pos ?? Vector2.zero;
        public Vector2 EnemyVel => Enemy?.CurrentState.Kinematics.Vel ?? Vector2.zero;
        public Vector2 EnemyForward => Enemy?.CurrentState.Kinematics.Forward ?? Vector2.up;
        public float EnemyHealthPct => Enemy?.CurrentState.HealthPct ?? 0f;
        public float EnemyShieldPct => Enemy?.CurrentState.ShieldPct ?? 0f;
        
        public Vector2 VectorToTarget => gunner?.VectorToTarget ?? Vector2.zero;
        public bool HasTargetLos => gunner?.HasTargetLos ?? false;
        public float AngleToTarget => gunner?.AngleToTarget ?? 0f;

        public bool IncomingMissile => false; //TODO
        public float LaserSpeed => 0f; //TODO
    }
}

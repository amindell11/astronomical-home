using NUnit.Framework;
using Movement.MPC;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests.EditMode
{
    [Category("MPC")]
    public class LosCostEditModeTests
    {
        private NativeArray<ObstacleData> obstacles;

        [TearDown]
        public void TearDown()
        {
            if (obstacles.IsCreated) obstacles.Dispose();
        }

        [Test]
        public void LosCost_Returns_Zero_When_No_Obstacles_Block()
        {
            obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
            obstacles[0] = new ObstacleData { position = new float2(10, 0), radius = 1f, weight = 1f };

            // Ship at origin, enemy at (0, 20) — obstacle far off to the side
            var cost = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 1);
            Assert.AreEqual(0f, cost);
        }

        [Test]
        public void LosCost_Returns_Positive_When_Obstacle_Blocks_Line()
        {
            obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
            // Obstacle directly between ship and enemy
            obstacles[0] = new ObstacleData { position = new float2(0, 10), radius = 2f, weight = 1f };

            var cost = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 1);
            Assert.Greater(cost, 0f);
        }

        [Test]
        public void LosCost_Higher_When_Obstacle_Center_Is_On_Line()
        {
            obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);

            // Obstacle dead center on line
            obstacles[0] = new ObstacleData { position = new float2(0, 10), radius = 2f, weight = 1f };
            var costCenter = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 1);

            // Obstacle slightly off-center (still blocking)
            obstacles[0] = new ObstacleData { position = new float2(1.5f, 10), radius = 2f, weight = 1f };
            var costOffset = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 1);

            Assert.Greater(costCenter, costOffset);
        }

        [Test]
        public void LosCost_TakesWorstBlocker_AndSaturates()
        {
            // LosCost is normalized [0,1] and reports the single worst (deepest) occluder
            // rather than summing. obstacle[0] is off-center (partial block), obstacle[1]
            // is dead-center (full block); adding the full blocker raises the cost to its
            // max without exceeding 1.
            obstacles = new NativeArray<ObstacleData>(2, Allocator.Temp);
            obstacles[0] = new ObstacleData { position = new float2(1.5f, 7), radius = 2f, weight = 1f };
            obstacles[1] = new ObstacleData { position = new float2(0, 14), radius = 2f, weight = 1f };

            var costPartialOnly = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 1);
            var costBoth = Cost.LosCost(float2.zero, new float2(0, 20), obstacles, 2);

            Assert.Greater(costBoth, costPartialOnly);
            Assert.LessOrEqual(costBoth, 1f);
        }

        [Test]
        public void ExposureCost_NearZero_When_Behind_Enemy()
        {
            // Enemy at (0, 10) facing +Y (yaw=0); ship at (0, 5) — directly behind.
            // The exposure arc is a Gaussian that asymptotes to (but never exactly reaches)
            // zero behind the enemy.
            var cost = Cost.ExposureCost(new float2(0, 5), new float2(0, 10), 0f);
            Assert.Less(cost, 0.01f);
        }

        [Test]
        public void ExposureCost_Returns_Max_When_Directly_In_Front()
        {
            // Enemy at (0, 10) facing +Y (yaw=0)
            // Ship at (0, 20) — directly in front
            var cost = Cost.ExposureCost(new float2(0, 20), new float2(0, 10), 0f);
            Assert.AreEqual(1f, cost, 0.01f);
        }

        [Test]
        public void ExposureCost_Low_To_The_Side()
        {
            // Enemy at origin facing +Y (yaw=0); ship at (10, 0) — perpendicular (90°).
            // The exposure arc is a Gaussian, so the side is low but non-zero and well below
            // a forward-arc hit (front at 0° = 1.0).
            var side = Cost.ExposureCost(new float2(10, 0), float2.zero, 0f);
            var front = Cost.ExposureCost(new float2(0, 10), float2.zero, 0f);
            Assert.Less(side, 0.15f);
            Assert.Less(side, front);
        }

        [Test]
        public void ExposureCost_Partial_When_In_Forward_Arc()
        {
            // Enemy at origin facing +Y (yaw=0)
            // Ship at (5, 10) — forward-right, ~26 degrees off center
            var cost = Cost.ExposureCost(new float2(5, 10), float2.zero, 0f);
            Assert.Greater(cost, 0f);
            Assert.Less(cost, 1f);
        }

        [Test]
        public void ExposureCost_Respects_Enemy_Yaw()
        {
            // Enemy at origin facing +X (yaw = -PI/2)
            var yaw = -math.PI / 2f;
            // Ship at (10, 0) — directly in front of enemy facing +X
            var costFront = Cost.ExposureCost(new float2(10, 0), float2.zero, yaw);
            // Ship at (-10, 0) — directly behind
            var costBehind = Cost.ExposureCost(new float2(-10, 0), float2.zero, yaw);

            Assert.AreEqual(1f, costFront, 0.01f);
            Assert.AreEqual(0f, costBehind, 0.01f);
        }
    }
}

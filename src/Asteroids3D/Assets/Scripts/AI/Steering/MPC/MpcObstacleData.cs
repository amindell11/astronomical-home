using UnityEngine;

namespace AI.Steering
{
    /// <summary>
    /// Represents a single obstacle for MPC collision avoidance
    /// </summary>
    public struct ObstacleInfo
    {
        public Vector2 position;
        public float radius;

        public ObstacleInfo(Vector2 pos, float r)
        {
            position = pos;
            radius = r;
        }
    }

    /// <summary>
    /// Collection of obstacles for MPC trajectory evaluation
    /// </summary>
    public class ObstacleData
    {
        public ObstacleInfo[] obstacles;
        public int count;

        public ObstacleData(int capacity = 32)
        {
            obstacles = new ObstacleInfo[capacity];
            count = 0;
        }

        public void Clear()
        {
            count = 0;
        }

        public void Add(Vector2 position, float radius)
        {
            if (count < obstacles.Length)
            {
                obstacles[count++] = new ObstacleInfo(position, radius);
            }
        }
    }
}

using AI.Scanning.Sensors;
using UnityEngine;

namespace Sensors
{
    public class FanSensor : IDirectionalSensor
    {
        private readonly Transform origin;
        private readonly float distance;
        private readonly float spreadAngle;
        private readonly float degreesBetweenRays;
        private readonly float sphereRadius;
        private readonly LayerMask layerMask;

        public Collider[] Buffer { get; }

        public FanSensor(
            Transform origin,
            float distance,
            float spreadAngle,
            float degreesBetweenRays,
            float sphereRadius,
            LayerMask layerMask,
            int bufferSize = 64)
        {
            this.origin = origin;
            this.distance = distance;
            this.spreadAngle = spreadAngle;
            this.degreesBetweenRays = degreesBetweenRays;
            this.sphereRadius = sphereRadius;
            this.layerMask = layerMask;
            Buffer = new Collider[bufferSize];
        }

        public int Detect()
        {
            return Detect(origin.up);
        }

        public int Detect(Vector3 direction)
        {
            return global::Sensors.RayFanSensor.Detect(
                origin.position,
                direction,
                distance,
                spreadAngle,
                degreesBetweenRays,
                sphereRadius,
                layerMask,
                Buffer
            );
        }
    }
}

using UnityEngine;

namespace AI.Scanning.Sensors
{
    public class SphereSensor : ISensor
    {
        private readonly Collider[] buffer;
        private readonly Transform origin;
        private readonly LayerMask layerMask;
        private readonly float radius;

        public Collider[] Buffer => buffer;

        public SphereSensor(Transform origin, float radius, LayerMask layerMask, int bufferSize = 64)
        {
            this.origin = origin;
            this.radius = radius;
            this.layerMask = layerMask;
            buffer = new Collider[bufferSize];
        }

        public int Detect()
        {
            return Physics.OverlapSphereNonAlloc(origin.position, radius, buffer, layerMask, QueryTriggerInteraction.Ignore);
        }
    }
}

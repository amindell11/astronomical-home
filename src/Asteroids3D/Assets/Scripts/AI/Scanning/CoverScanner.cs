using AI.Scanning.Sensors;
using UnityEngine;

namespace AI.Scanning
{
    public class CoverScanner
    {
        private readonly SphereSensor sensor;
        public bool HasCover { get; private set; }

        public CoverScanner(Transform origin, float radius, LayerMask asteroidMask)
        {
            sensor = new SphereSensor(origin, radius, asteroidMask, bufferSize: 8);
        }

        public void Scan()
        {
            HasCover = sensor.Detect() > 0;
        }
    }
}

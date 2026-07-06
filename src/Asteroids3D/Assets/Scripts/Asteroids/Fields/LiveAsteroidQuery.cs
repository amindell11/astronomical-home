using Asteroids;
using UnityEngine;

namespace Asteroids.Fields
{
    public readonly struct LiveAsteroidQueryHit
    {
        public readonly AsteroidController asteroid;
        public readonly Vector2 planePosition;
        public readonly float radius;
        public readonly Collider collider;

        public LiveAsteroidQueryHit(AsteroidController asteroid, Vector2 planePosition, float radius, Collider collider)
        {
            this.asteroid = asteroid;
            this.planePosition = planePosition;
            this.radius = radius;
            this.collider = collider;
        }
    }
}

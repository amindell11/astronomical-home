using UnityEngine;

namespace Asteroids.Fragnetics
{
    public struct Frag
    {
        public float Mass;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 Spin;

        public Frag(float mass, Vector3 position, Quaternion rotation)
        {
            Mass = mass;
            Position = position;
            Rotation = rotation;
            Velocity = Vector3.zero;
            Spin = Vector3.zero;
        }
    }
        
    public struct FragSum
    {
        public float totalMass;
        public Vector3 pFrag;
        public Vector3 mrSum;
        public Vector3 lOrbit;
        public float iTotal;
    }
    
    public readonly struct HitData
    {
        public readonly float Mass;
        public readonly Vector3 Velocity;
        public readonly Vector3 HitPoint;

        public HitData(float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
        {
            HitPoint = hitPoint;
            Mass = projectileMass;
            Velocity = projectileVelocity;
        }
    }

    public readonly struct AsteroidData
    {
        public AsteroidData(float mass, Quaternion rotation, Vector3 angularVelocity, Vector3 velocity,
            Vector3 position, Vector3 inertiaTensor)
        {
            Mass = mass;
            Rotation = rotation;
            AngularVelocity = angularVelocity;
            Velocity = velocity;
            Position = position;
            InertiaTensor = inertiaTensor;
        }
        public AsteroidData(AsteroidController ast) : 
            this(ast.Mass, ast.transform.rotation, ast.Rb.angularVelocity, 
                ast.Rb.linearVelocity, ast.transform.position, ast.Rb.inertiaTensor) {}
        
        public readonly float Mass;
        public readonly Quaternion Rotation;
        public readonly Vector3 AngularVelocity;
        public readonly Vector3 InertiaTensor;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
    }
}

using UnityEngine;

namespace AI.Steering
{
    public readonly struct Dynamics
    {
        public readonly float mass;
        public readonly float maxSpeed;
        public readonly float maxYawRate;
        public readonly float forwardAcc;
        public readonly float reverseAcc;
        public readonly float maxStrafeAcc;
        public readonly float minStrafeAcc;
        public readonly float yawTorque;
        public readonly float angularDrag;
        public readonly float linearDrag;



        public Dynamics(float mass, float forwardAcc, float reverseAcc, float maxStrafeAcc, float minStrafeAcc, float maxSpeed,  float maxYawRate, float yawTorque, float angularDrag, float linearDrag)
        {
            this.mass = mass;
            this.forwardAcc  = forwardAcc;
            this.reverseAcc  = reverseAcc;
            this.maxStrafeAcc   = maxStrafeAcc;
            this.minStrafeAcc   = minStrafeAcc;
            this.maxSpeed = maxSpeed;
            this.maxYawRate = maxYawRate;
            this.yawTorque = yawTorque;
            this.angularDrag = angularDrag;
            this.linearDrag = linearDrag;
        }

        public static readonly Dynamics Default = new Dynamics(
            mass: 200,
            forwardAcc: 8f,
            reverseAcc: 4f,
            maxStrafeAcc:  6f,
            minStrafeAcc:  4f,
            maxSpeed: 20f,
            maxYawRate: 10f,
            yawTorque: 1f,
            angularDrag: 0.1f,
            linearDrag: 0.1f
            );
    }
} 
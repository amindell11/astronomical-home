using System;
using Game;
using UnityEngine;

namespace Movement
{
    [DefaultExecutionOrder(-100)]
    public class KinematicsPoller : MonoBehaviour
    {
        private Rigidbody rb;
        private IGamePlane plane;

        public Kinematics Kinematics { get; private set; }

        public void SetPlane(IGamePlane planeProvider)
        {
            plane = planeProvider ?? throw new ArgumentNullException(nameof(planeProvider));
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (plane == null)
                return;

            Kinematics = GetStateFrom3D();
        }

        private Kinematics GetStateFrom3D()
        {
            var frame = plane.CurrentFrame;
            var pos = frame.ToPlanePoint(transform.position);
            var vel = frame.ToPlaneVector(rb.linearVelocity);
            var yaw = Vector3.SignedAngle(frame.Forward, transform.up, frame.Normal);
            var yawRate = Vector3.Dot(rb.angularVelocity, frame.Normal) * Mathf.Rad2Deg;
            var bank = Vector3.SignedAngle(frame.Normal, transform.forward, transform.up);
            return new Kinematics(pos, vel, yaw, yawRate, bank);
        }
    }
}

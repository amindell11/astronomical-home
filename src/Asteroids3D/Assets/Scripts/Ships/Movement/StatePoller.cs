using Game;
using UnityEngine;

namespace Ships.Movement
{
    public class StatePoller : MonoBehaviour
    {
        private Rigidbody rb;
        public Kinematics Kinematics { get; private set; }
        private void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate() {
            Kinematics = GetStateFrom3D();
        }
            
        private Kinematics GetStateFrom3D() {
            var pos = GamePlane.WorldPointToPlane(transform.position);
            var vel = GamePlane.WorldPointToPlane(rb.linearVelocity);
            var yaw = Vector3.SignedAngle(GamePlane.Forward, transform.up, GamePlane.Normal);
            var yawRate = Vector3.Dot(rb.angularVelocity, GamePlane.Normal) * Mathf.Rad2Deg;
            var bank = Vector3.SignedAngle(GamePlane.Normal, transform.forward, transform.up);
            return new Kinematics(pos, vel, yaw, yawRate, bank);
        }   
    }
}
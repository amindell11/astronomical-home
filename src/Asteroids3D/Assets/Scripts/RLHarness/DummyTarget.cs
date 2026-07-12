using UnityEngine;

namespace Game.RLHarness
{
    public class DummyTarget : MonoBehaviour
    {
        private Vector2 planePosition;
        private Vector2 planeForward = Vector2.up;
        private bool aimEnabled;
        private float aimRateDegPerSec;
        private Transform aimTarget;

        public Vector2 PlanePosition => planePosition;
        public Vector2 PlaneForward => planeForward;

        public void Configure(Vector2 planePos, Vector2 initialForward, bool aim, float aimRateDegPerSec)
        {
            planePosition = planePos;
            planeForward = initialForward.sqrMagnitude > 1e-6f ? initialForward.normalized : Vector2.up;
            aimEnabled = aim;
            this.aimRateDegPerSec = aimRateDegPerSec;
            SyncTransform();
        }

        public void SetAimTarget(Transform target) => aimTarget = target;

        private void FixedUpdate()
        {
            if (!aimEnabled || !aimTarget) return;

            var shipPlane = GamePlane.WorldPointToPlane(aimTarget.position);
            var toShip = shipPlane - planePosition;
            if (toShip.sqrMagnitude < 1e-6f) return;

            var currentDeg = Mathf.Atan2(planeForward.y, planeForward.x) * Mathf.Rad2Deg;
            var desiredDeg = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg;
            var step = Mathf.Clamp(Mathf.DeltaAngle(currentDeg, desiredDeg),
                -aimRateDegPerSec * Time.fixedDeltaTime, aimRateDegPerSec * Time.fixedDeltaTime);
            var newDeg = (currentDeg + step) * Mathf.Deg2Rad;
            planeForward = new Vector2(Mathf.Cos(newDeg), Mathf.Sin(newDeg));
            SyncTransform();
        }

        private void SyncTransform()
        {
            transform.position = GamePlane.PlanePointToWorld(planePosition);
            transform.rotation = GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(planeForward));
        }
    }
}

using Substrate;
using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// Glides across the game plane at a fixed velocity with nothing to follow, so the locale sky's
    /// layers parallax as they do under the observer camera. The hangar's backdrop camera.
    /// </summary>
    public sealed class DriftCam : CameraController
    {
        [Tooltip("Glide velocity in world units per second along the game plane (x = right, y = forward).")]
        [SerializeField] private Vector2 driftVelocity = new(4f, 1.5f);

        protected override void Awake()
        {
            base.Awake();
            transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
        }

        private void Update() =>
            transform.position += GamePlane.PlaneDirToWorld(driftVelocity) * Time.unscaledDeltaTime;
    }
}

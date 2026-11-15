using Cameras;
using FMODUnity;
using Substrate;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// The camera's FMOD listener. It sends the camera's in-plane follow velocity so doppler follows motion
    /// on screen. Unity calls only this Update, so it replaces StudioListener's own write, which
    /// sends zero velocity without a Rigidbody, and a separate component would race that write.
    /// It stays a StudioListener so FMOD's listener count and one-shot distance culling still find it.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1581
    /// </summary>
    [DefaultExecutionOrder(100)] // after SmoothCamera's Update, so Velocity is this frame's
    public sealed class CameraListener : StudioListener
    {
        [Tooltip("The camera whose follow velocity the listener sends.")]
        [SerializeField] private SmoothCamera view;

        // The pan point sits over the plane point beneath the camera, so only in-plane motion moves it.
        private void Update() => RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, AttenuationObject,
            GamePlane.PlaneDirToWorld(GamePlane.WorldDirToPlane(view.Velocity)));
    }
}

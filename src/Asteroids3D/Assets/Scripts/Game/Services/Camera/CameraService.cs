using Cameras;
using Player;
using UnityEngine;

namespace Game.Services
{
    public class CameraService : ICameraService
    {
        public CameraRig CameraRig { get; private set; }
        public Camera MainCamera => CameraRig ? CameraRig.MainCamera : null;
        public Camera UICamera => CameraRig ? CameraRig.UICamera : null;

        public void Initialize(CameraRig prefab)
        {
            if (!prefab) return;
            Clear();
            CameraRig = Object.Instantiate(prefab);
        }

        public void SetSubject(Transform subject)
        {
            if (!CameraRig || !subject) return;
            CameraRig.ObserverCam.SetSubject(subject);
        }

        public void AddSecondarySubject(Transform subject)
        {
            if (!CameraRig || !subject) return;
            CameraRig.ObserverCam.AddSecondarySubject(subject);
        }

        public void RemoveSecondarySubject(Transform subject)
        {
            if (!CameraRig || !subject) return;
            CameraRig.ObserverCam.RemoveSecondarySubject(subject);
        }

        public void ConfigurePlayerInputProjection(PlayerCommander commander)
        {
            if (commander == null || !CameraRig) return;

            commander.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(CameraRig.MainCamera.ScreenToWorldPoint(pos))
                + GamePlane.Origin);
        }

        public void Clear()
        {
            if (CameraRig != null)
            {
                Object.Destroy(CameraRig.gameObject);
                CameraRig = null;
            }
        }
    }
}

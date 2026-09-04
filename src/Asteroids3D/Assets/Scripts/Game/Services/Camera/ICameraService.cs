using System.Collections.Generic;
using Cameras;
using UnityEngine;

namespace Game.Services.Camera
{
    public interface ICameraService
    {
        Dictionary<CameraTag, CameraController> Cameras { get; }

        T GetCamera<T>(CameraTag tag) where T : CameraController;

        /// <summary>Instantiate the camera rig from a prefab.</summary>
        void Initialize();

        void AddCamera(CameraTag tag, CameraController camera);
        void RemoveCamera(CameraTag tag);

        /// <summary>Destroy the camera rig and reset state.</summary>
        void Clear();
    }
}

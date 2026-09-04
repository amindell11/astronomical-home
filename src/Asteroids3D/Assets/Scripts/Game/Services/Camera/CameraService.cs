using System.Collections.Generic;
using System.Linq;
using Cameras;
using UnityEngine;

namespace Game.Services.Camera
{
    public class CameraService : ICameraService
    {
        public Dictionary<CameraTag, CameraController> Cameras { get; } = new();

        public T GetCamera<T>(CameraTag tag) where T : CameraController =>
            Cameras.TryGetValue(tag, out var cam) ? cam as T : null;

        public void AddCamera(CameraTag tag, CameraController camera) => Cameras[tag] = camera;

        public void RemoveCamera(CameraTag tag) => Cameras.Remove(tag);

        public void Initialize()
        {
        }

        public void Clear()
        {
            foreach (var cam in Cameras.Values.Where(cam => cam)) Object.Destroy(cam.gameObject);
            Cameras.Clear();
        }
    }
}

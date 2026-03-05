using System.Collections.Generic;
using Cameras;
using UnityEngine;

namespace Game.Services
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

        public void Clear() => Cameras.Clear();
    }
}

using UnityEngine;

namespace Cameras.Shake
{
    public interface IShakeListener
    {
        void Hear(Vector3 position, CameraShakeProfile profile, float size);
    }
}

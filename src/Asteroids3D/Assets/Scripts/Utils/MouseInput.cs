using Game;
using UnityEngine;

namespace Utils
{
    public class MouseInput : MonoSingleton<MouseInput>
    {
        private Camera mainCamera;

        private void Awake()
        {
            base.Awake();
            mainCamera = Camera.main;
        }
        public Vector3 GetMouseWorldPosition()
        {
            return GamePlane.ProjectOntoPlane(
                mainCamera.ScreenToWorldPoint(
                    UnityEngine.Input.mousePosition));
        }
    }
}
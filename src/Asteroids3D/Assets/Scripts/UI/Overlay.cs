using System.Linq;
using Game;
using UnityEngine;
using Utils;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        private void Start()
        {
            var canvas = GetComponent<Canvas>();
            var mainCamera = ServiceLocator.Get<Camera>();
            var uiCamera = mainCamera.GetComponentsInChildren<Camera>()
                .FirstOrDefault(t => t.CompareTag(TagNames.UICam));
            
            if (uiCamera != null)
            {
                canvas.worldCamera = uiCamera;
            }
            else
            {
                Debug.LogWarning("UI Camera not found. Canvas may not function correctly.");
            }
        }
    }
}

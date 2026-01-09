using System.Linq;
using Game;
using UnityEngine;
using Utils;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        private Canvas canvas;
        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        public void SetCanvasWorldCamera(Camera uicam)
        {
            canvas.worldCamera = uicam;
        }
    }
}

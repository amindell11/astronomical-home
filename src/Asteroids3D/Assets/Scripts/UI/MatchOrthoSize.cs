using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(Camera))]
    public class MatchOrthoSize : MonoBehaviour
    {
        [Tooltip("Camera to copy size from (leave empty = main camera)")]
        [SerializeField] private Camera source;

        private Camera self;

        void Awake()
        {
            self   = GetComponent<Camera>();
            source = source ? source : Camera.main;
        }

        void LateUpdate()                        // after any zoom logic has run
        {
            if (source && self.orthographic != source.orthographic)
                self.orthographic = source.orthographic;

            if (source && self.orthographic)
                self.orthographicSize = source.orthographicSize;
        }
    }
}
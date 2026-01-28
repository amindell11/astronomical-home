using UnityEngine;
using UnityEngine.Serialization;

namespace World
{
    public class WorldFollow : MonoBehaviour
    {
        private Transform target;
        [SerializeField] private Vector3 offset;
        [SerializeField] private float smoothSpeed = 5f;
    
        private void FixedUpdate()
        {
            if (!target) return;
            var desiredPosition = target.position + offset;
            var smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.fixedDeltaTime);
            transform.position = smoothedPosition;
        }
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }
    }
} 
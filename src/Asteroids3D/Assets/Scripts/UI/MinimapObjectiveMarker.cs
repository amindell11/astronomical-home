using Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Positions a UI icon over the minimap RawImage to mark the current objective's
    /// world position. Attach as a child of the minimap RawImage in the overlay prefab.
    /// </summary>
    public class MinimapObjectiveMarker : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScale = 0.15f;
        [SerializeField] private Color baseColor = Color.yellow;

        private RectTransform minimapRect;
        private Camera minimapCam;
        private Transform target;

        public void Initialize(Camera cam, RectTransform minimap)
        {
            minimapCam = cam;
            minimapRect = minimap;
            if (icon) icon.enabled = false;
        }

        public void SetTarget(Transform objective)
        {
            target = objective;
            if (icon) icon.enabled = target;
        }

        private void LateUpdate()
        {
            if (!target || !minimapCam || !icon)
            {
                if (icon) icon.enabled = false;
                return;
            }

            var vp = minimapCam.WorldToViewportPoint(target.position);
            vp.x = Mathf.Clamp01(vp.x);
            vp.y = Mathf.Clamp01(vp.y);

            icon.rectTransform.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * minimapRect.rect.width,
                (vp.y - 0.5f) * minimapRect.rect.height);

            float t = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            icon.color = Color.Lerp(baseColor, Color.white, t);
            icon.rectTransform.localScale = Vector3.one * (1f + pulseScale * Mathf.Sin(Time.time * pulseSpeed));

            icon.enabled = true;
        }
    }
}

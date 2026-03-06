using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Positions a UI icon over the minimap RawImage to mark the current objective's
    /// world position. Attach as a child of the minimap RawImage in the overlay prefab.
    /// The parent RectTransform is used as the minimap bounds.
    /// </summary>
    public class MinimapObjectiveMarker : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private float iconSize = 12f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScale = 0.15f;
        [SerializeField] private Color baseColor = Color.yellow;

        private RectTransform minimapRect;
        private Camera minimapCam;
        private Transform target;

        public void Initialize(Camera cam)
        {
            minimapCam = cam;
            minimapRect = transform.parent as RectTransform;

            if (icon)
            {
                var rt = icon.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(iconSize, iconSize);
                icon.enabled = false;
            }
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

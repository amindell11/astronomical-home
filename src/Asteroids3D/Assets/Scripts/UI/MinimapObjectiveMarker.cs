using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Positions a UI icon over the minimap RawImage to mark the current objective's
    /// world position. Place anywhere in the overlay; pass the minimap RectTransform
    /// and camera via Initialize.
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

        private readonly Vector3[] corners = new Vector3[4];

        private void LateUpdate()
        {
            if (!target || !minimapCam || !minimapRect || !icon)
            {
                if (icon) icon.enabled = false;
                return;
            }

            var vp = minimapCam.WorldToViewportPoint(target.position);
            vp.x = Mathf.Clamp01(vp.x);
            vp.y = Mathf.Clamp01(vp.y);

            // Map viewport to minimap rect in world space (scale-independent)
            minimapRect.GetWorldCorners(corners); // 0=BL, 1=TL, 2=TR, 3=BR
            var pos = Vector3.Lerp(
                Vector3.Lerp(corners[0], corners[3], vp.x),
                Vector3.Lerp(corners[1], corners[2], vp.x),
                vp.y);
            icon.rectTransform.position = pos;

            float t = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            icon.color = Color.Lerp(baseColor, Color.white, t);
            icon.rectTransform.localScale = Vector3.one * (1f + pulseScale * Mathf.Sin(Time.time * pulseSpeed));

            icon.enabled = true;
        }
    }
}

using Game.Services;
using Objectives;
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

        private IObjectiveService objectives;

        public void Initialize(Camera cam, RectTransform minimap)
        {
            minimapCam = cam;
            minimapRect = minimap;
            if (icon) icon.enabled = false;
        }

        /// <summary>
        /// Bind to the objective service: subscribe to target changes and self-decide visibility by
        /// the service's CurrentState. Replaces the sector acting as the marker's bridge.
        /// </summary>
        public void BindObjectiveService(IObjectiveService service)
        {
            if (objectives != null) objectives.OnTargetChanged -= OnObjectiveTargetChanged;
            objectives = service;
            if (objectives == null) return;
            objectives.OnTargetChanged += OnObjectiveTargetChanged;
            OnObjectiveTargetChanged(objectives.CurrentTarget);
        }

        private void OnObjectiveTargetChanged(Transform t) => target = t;

        private void OnDestroy()
        {
            if (objectives != null) objectives.OnTargetChanged -= OnObjectiveTargetChanged;
        }

        private static bool IsShowingState(ObjectiveType? state) =>
            state == ObjectiveType.Explore ||
            state == ObjectiveType.KeyAcquired ||
            state == ObjectiveType.ExtractionChallenge;

        private void LateUpdate()
        {
            // Show the objective target only during objective states that have one.
            var effective = IsShowingState(objectives?.CurrentState) ? target : null;

            if (!effective || !minimapCam || !icon)
            {
                if (icon) icon.enabled = false;
                return;
            }

            var vp = minimapCam.WorldToViewportPoint(effective.position);
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

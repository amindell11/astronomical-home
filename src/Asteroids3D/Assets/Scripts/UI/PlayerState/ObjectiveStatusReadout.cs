using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    [RequireComponent(typeof(Text))]
    public sealed class ObjectiveStatusReadout : MonoBehaviour
    {
        private Text label;
        private IObjectiveService objectives;

        public static ObjectiveStatusReadout Create(Transform parent)
        {
            var go = new GameObject("ObjectiveStatusReadout", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(420f, 28f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            return go.AddComponent<ObjectiveStatusReadout>();
        }

        private void Awake()
        {
            label = GetComponent<Text>();
        }

        public void Initialize(IObjectiveService objectives)
        {
            this.objectives = objectives;
            Apply();
        }

        private void Update() => Apply();

        // Text's setter skips an unchanged string, so the per-frame poll costs one compare.
        private void Apply() => label.text = objectives.SpineTracker?.CurrentStatusLine ?? string.Empty;
    }
}

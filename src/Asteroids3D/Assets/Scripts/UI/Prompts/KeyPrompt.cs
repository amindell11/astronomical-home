using UnityEngine;
using UnityEngine.UI;

namespace UI.Prompts
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        private const int WordSize = 12;
        private const int KeySize = 10;
        private const float Height = 18f;
        private const float Gap = 5f;
        private const float CapPadding = 4f;
        private const float CapHeight = 15f;
        private const float Edge = 1f;

        private Text press;
        private Text key;
        private Text action;
        private RectTransform cap;

        public string Key => key.text;
        public string Action { get; private set; }

        public static KeyPrompt Create(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            string key, string action)
        {
            var go = new GameObject("KeyPrompt", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var prompt = go.AddComponent<KeyPrompt>();
            prompt.press = CreateText(go.transform, "Press", wordFont, textMaterial, WordSize);
            prompt.press.text = "PRESS";

            prompt.cap = (RectTransform)new GameObject("Key cap", typeof(RectTransform)).transform;
            prompt.cap.gameObject.layer = go.layer;
            prompt.cap.SetParent(go.transform, false);
            CreateEdge(prompt.cap, "Top", new Vector2(0f, 1f), Vector2.one);
            CreateEdge(prompt.cap, "Bottom", Vector2.zero, new Vector2(1f, 0f));
            CreateEdge(prompt.cap, "Left", Vector2.zero, new Vector2(0f, 1f));
            CreateEdge(prompt.cap, "Right", new Vector2(1f, 0f), Vector2.one);
            prompt.key = CreateText(prompt.cap, "Key", keyFont, textMaterial, KeySize);
            prompt.key.rectTransform.anchorMin = Vector2.zero;
            prompt.key.rectTransform.anchorMax = Vector2.one;
            prompt.key.rectTransform.sizeDelta = Vector2.zero;

            prompt.action = CreateText(go.transform, "Action", wordFont, textMaterial, WordSize);
            prompt.Set(key, action);
            return prompt;
        }

        public void Set(string key, string action)
        {
            if (key == Key && action == Action) return;
            this.key.text = key;
            Action = action;
            this.action.text = $"TO {action}";

            var pressWidth = press.preferredWidth;
            var capWidth = this.key.preferredWidth + 2f * CapPadding;
            var actionWidth = this.action.preferredWidth;
            var width = pressWidth + capWidth + actionWidth + 2f * Gap;
            ((RectTransform)transform).sizeDelta = new Vector2(width, Height);
            var x = -width / 2f;
            Place(press.rectTransform, ref x, pressWidth, Height);
            Place(cap, ref x, capWidth, CapHeight);
            Place(this.action.rectTransform, ref x, actionWidth, Height);
        }

        private static void Place(RectTransform piece, ref float x, float width, float height)
        {
            piece.anchorMin = piece.anchorMax = new Vector2(.5f, .5f);
            piece.pivot = new Vector2(0f, .5f);
            piece.anchoredPosition = new Vector2(x, 0f);
            piece.sizeDelta = new Vector2(width, height);
            x += width + Gap;
        }

        private static Text CreateText(Transform parent, string name, Font font, Material material, int size)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = parent.gameObject.layer;
            text.transform.SetParent(parent, false);
            text.font = font;
            text.material = material;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // Side edges stop short of the top and bottom so faded corners never double-blend.
        private static void CreateEdge(RectTransform cap, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var edge = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            edge.gameObject.layer = cap.gameObject.layer;
            edge.transform.SetParent(cap, false);
            var vertical = anchorMin.x == anchorMax.x;
            edge.rectTransform.anchorMin = anchorMin;
            edge.rectTransform.anchorMax = anchorMax;
            edge.rectTransform.pivot = (anchorMin + anchorMax) / 2f;
            edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.rectTransform.sizeDelta = vertical ? new Vector2(Edge, -2f * Edge) : new Vector2(0f, Edge);
            edge.color = Color.white;
            edge.raycastTarget = false;
        }
    }
}

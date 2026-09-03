using System;
using System.Collections.Generic;
using System.Text;
using Damage;
using Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Post-death recap panel rendered from the damage ledger: what killed you, and what hurt
    /// you this life, aggregated per source. Code-built (no prefab) so headless paths never
    /// touch it; the driver creates it during <see cref="Game.Session.GameState.DeathRecap"/>.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DeathRecapScreen : MonoBehaviour
    {
        private const int MaxRows = 6;

        public static DeathRecapScreen Create()
        {
            var go = new GameObject("DeathRecapScreen");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            go.AddComponent<GraphicRaycaster>();
            return go.AddComponent<DeathRecapScreen>();
        }

        public void Show(in DamageInfo killingBlow, IReadOnlyList<DamageLedger.Row> rows, Action onContinue)
        {
            EnsureEventSystem();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var dim = AddStretchedImage(transform, "Dim", new Color(0f, 0f, 0f, 0.65f));

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(dim.transform, false);
            panel.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.9f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel.transform, "Title", "SHIP DESTROYED", font, 34,
                new Color(1f, 0.45f, 0.35f), FontStyle.Bold);
            AddText(panel.transform, "Cause", CauseLine(killingBlow, rows), font, 22, Color.white);
            AddText(panel.transform, "Rows", RowsBlock(rows), font, 17, new Color(0.8f, 0.83f, 0.88f));

            AddContinueButton(panel.transform, font, onContinue);
        }

        internal static string CauseLine(in DamageInfo killingBlow, IReadOnlyList<DamageLedger.Row> rows)
        {
            if (killingBlow.Kind == DamageKind.Collision)
                return "You flew into an asteroid.";

            var name = SourceNameFor(killingBlow, rows);
            return name != null
                ? $"Destroyed by {name} — {DamageLedger.DescribeKind(killingBlow.Kind)}."
                : $"Destroyed by {DamageLedger.DescribeKind(killingBlow.Kind)}.";
        }

        internal static string RowsBlock(IReadOnlyList<DamageLedger.Row> rows)
        {
            if (rows == null || rows.Count == 0) return "No damage recorded this life.";

            var sorted = new List<DamageLedger.Row>(rows);
            sorted.Sort((a, b) => b.Total.CompareTo(a.Total));

            var sb = new StringBuilder("Damage taken this life:\n");
            var shown = Mathf.Min(sorted.Count, MaxRows);
            for (var i = 0; i < shown; i++)
            {
                var row = sorted[i];
                sb.Append($"\n{row.SourceName} — {DamageLedger.DescribeKind(row.Kind)}: " +
                          $"{row.Total:0} dmg ({row.Hits} {(row.Hits == 1 ? "hit" : "hits")})");
            }
            if (sorted.Count > shown)
                sb.Append($"\n… and {sorted.Count - shown} more");
            return sb.ToString();
        }

        private static string SourceNameFor(in DamageInfo killingBlow, IReadOnlyList<DamageLedger.Row> rows)
        {
            if (rows == null) return null;
            for (var i = 0; i < rows.Count; i++)
                if (rows[i].AttackerId == killingBlow.AttackerId && rows[i].Kind == killingBlow.Kind)
                    return rows[i].SourceName;
            return null;
        }

        private static GameObject AddStretchedImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;

            // Center anchor for children laid out inside the stretched dim.
            var center = go.AddComponent<VerticalLayoutGroup>();
            center.childAlignment = TextAnchor.MiddleCenter;
            center.childControlWidth = false;
            center.childControlHeight = false;
            center.childForceExpandWidth = false;
            center.childForceExpandHeight = false;
            return go;
        }

        private static Text AddText(Transform parent, string name, string content, Font font,
            int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        private static void AddContinueButton(Transform parent, Font font, Action onContinue)
        {
            var go = new GameObject("Continue", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.95f, 1f);
            var size = go.AddComponent<LayoutElement>();
            size.preferredWidth = 220f;
            size.preferredHeight = 44f;
            var label = AddText(go.transform, "Label", "CONTINUE", font, 20, Color.white, FontStyle.Bold);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Button>().onClick.AddListener(() => onContinue?.Invoke());
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}

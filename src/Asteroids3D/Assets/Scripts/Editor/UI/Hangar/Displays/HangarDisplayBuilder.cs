using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    public static class HangarDisplayBuilder
    {
        private const string FontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";

        public static HangarMainDisplayView BuildMain(Transform parent, Vector3 localPosition, Vector2Int resolution,
            UnityEngine.Object feature)
        {
            var display = Rig(parent, "MainDisplay", localPosition, resolution, feature, out var canvas);
            LayoutMain(canvas, out var buttons, out var labels);
            var view = display.gameObject.AddComponent<HangarMainDisplayView>();
            var data = new SerializedObject(view);
            Assign(data.FindProperty("buttons"), buttons);
            Assign(data.FindProperty("labels"), labels);
            data.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        public static HangarDetailDisplayView BuildDetail(Transform parent, Vector3 localPosition, Vector2Int resolution,
            UnityEngine.Object feature)
        {
            var display = Rig(parent, "DetailDisplay", localPosition, resolution, feature, out var canvas);
            LayoutDetail(canvas, out var title, out var body);
            var view = display.gameObject.AddComponent<HangarDetailDisplayView>();
            var data = new SerializedObject(view);
            data.FindProperty("title").objectReferenceValue = title;
            data.FindProperty("body").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static HangarDisplay Rig(Transform parent, string name, Vector3 localPosition, Vector2Int resolution,
            UnityEngine.Object feature, out RectTransform canvasRect)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            canvasRect = canvas.GetComponent<RectTransform>();
            HangarDisplay.Configure(camera, canvas, scaler, resolution);

            var imageObject = new GameObject("CanopyImage");
            imageObject.transform.SetParent(root.transform, false);
            var image = imageObject.AddComponent<CanopyImage>();
            var imageData = new SerializedObject(image);
            imageData.FindProperty("feature").objectReferenceValue = feature;
            imageData.ApplyModifiedPropertiesWithoutUndo();

            var display = root.AddComponent<HangarDisplay>();
            var data = new SerializedObject(display);
            data.FindProperty("displayCamera").objectReferenceValue = camera;
            data.FindProperty("canvas").objectReferenceValue = canvas;
            data.FindProperty("image").objectReferenceValue = image;
            data.FindProperty("resolution").vector2IntValue = resolution;
            data.ApplyModifiedPropertiesWithoutUndo();
            return display;
        }

        private static void LayoutMain(RectTransform canvas, out Button[] buttons, out Text[] labels)
        {
            var font = Font();
            var slots = new[] { "Ship", "Weapon", "Ordnance" };
            buttons = new Button[slots.Length];
            labels = new Text[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var buttonObject = new GameObject(slots[i], typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(canvas, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f - (i + 1) / 3f);
                rect.anchorMax = new Vector2(1f, 1f - i / 3f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.16f, 0.17f, 0.19f, 1f);
                var button = buttonObject.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                buttons[i] = button;
                labels[i] = Label(buttonObject.transform, font, 32, TextAnchor.MiddleCenter);
            }
        }

        private static void LayoutDetail(RectTransform canvas, out Text title, out Text body)
        {
            var font = Font();
            title = Block(canvas, "Title", font, 36, TextAnchor.MiddleCenter, new Vector2(0f, .72f), Vector2.one);
            body = Block(canvas, "Body", font, 22, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1f, .72f));
        }

        private static Text Block(RectTransform parent, string name, Font font, int size, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(24f, 12f);
            rect.offsetMax = new Vector2(-24f, -12f);
            return Label(row.transform, font, size, alignment);
        }

        private static Text Label(Transform parent, Font font, int size, TextAnchor alignment)
        {
            var text = parent.GetComponent<Text>();
            if (!text)
            {
                var row = new GameObject("Label", typeof(RectTransform));
                row.transform.SetParent(parent, false);
                var rect = row.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                text = row.AddComponent<Text>();
            }
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font Font()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (!font) throw new System.InvalidOperationException($"Hangar displays need the font at {FontPath}.");
            return font;
        }

        private static void Assign(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}

using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// Builds both display rigs and authors their layouts in canvas pixels, top-left origin, on
    /// the grid the screen design was drawn on (twice the reference picture).
    /// </summary>
    public static class HangarDisplayBuilder
    {
        private const string LabelFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
        private const string TitleFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string NumberFontPath = "Assets/Visuals/Ui/Fonts/IBMPlexMono.ttf";
        private const int Segments = 12;
        private const float SegmentWidth = 9f;
        private const float SegmentGap = 3f;
        private const float BarHeight = 8f;
        private const float BarWidth = Segments * SegmentWidth + (Segments - 1) * SegmentGap;

        private static readonly Color Amber = new(1f, .80f, .50f, 1f);
        private static readonly Color Label = new(.62f, .70f, .78f, 1f);
        private static readonly Color Line = new(.45f, .58f, .68f, 1f);
        private static readonly Color Unlit = new(.26f, .31f, .37f, 1f);
        private static readonly Color Locked = new(1f, .42f, .32f, 1f);
        private static readonly Color Clear = new(0f, 0f, 0f, 0f);

        public static HangarMainDisplayView BuildMain(Transform parent, Vector3 localPosition, Vector2Int resolution,
            Object feature)
        {
            var display = Rig(parent, "MainDisplay", localPosition, resolution, feature, out var canvas);
            var view = display.gameObject.AddComponent<HangarMainDisplayView>();
            LayoutMain(canvas, resolution.x, new SerializedObject(view));
            return view;
        }

        public static HangarDetailDisplayView BuildDetail(Transform parent, Vector3 localPosition, Vector2Int resolution,
            Object feature)
        {
            var display = Rig(parent, "DetailDisplay", localPosition, resolution, feature, out var canvas);
            LayoutDetail(canvas, resolution.x, out var title, out var body);
            var view = display.gameObject.AddComponent<HangarDetailDisplayView>();
            var data = new SerializedObject(view);
            data.FindProperty("title").objectReferenceValue = title;
            data.FindProperty("body").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static HangarDisplay Rig(Transform parent, string name, Vector3 localPosition, Vector2Int resolution,
            Object feature, out RectTransform canvasRect)
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

        private static void LayoutMain(RectTransform canvas, float width, SerializedObject view)
        {
            var labels = Font(LabelFontPath);
            var titles = Font(TitleFontPath);
            var numbers = Font(NumberFontPath);

            var slots = new[]
            {
                (name: "Ship", rect: new Rect(190f, 6f, 360f, 420f)),
                (name: "Weapon", rect: new Rect(120f, 456f, 160f, 120f)),
                (name: "Ordnance", rect: new Rect(310f, 456f, 160f, 120f))
            };
            var buttons = new Object[slots.Length];
            var brackets = new Object[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var button = Box(canvas, slots[i].name, slots[i].rect).gameObject;
                var face = button.AddComponent<Image>();
                face.color = Clear;
                face.raycastTarget = true;
                var click = button.AddComponent<Button>();
                click.targetGraphic = face;
                click.transition = Selectable.Transition.None;
                buttons[i] = click;
                brackets[i] = Bracket(canvas, slots[i].name + "Bracket", slots[i].rect);
            }

            var title = Text(canvas, "Title", titles, 40, TextAnchor.MiddleCenter, Amber, new Rect(0f, 12f, width, 52f));
            var lockTag = Text(canvas, "LockTag", labels, 24, TextAnchor.MiddleCenter, Locked, new Rect(0f, 66f, width, 26f));
            lockTag.text = "LOCKED";
            lockTag.gameObject.SetActive(false);
            var bars = new Object[HangarStats.Count];
            var rows = new[] { 104f, 208f, 312f };
            var hullPoints = new[] { new Vector2(300f, 226f), new Vector2(292f, 292f), new Vector2(300f, 356f) };
            for (var i = 0; i < HangarStats.Count; i++)
            {
                var stat = (HangarStat)i;
                var right = i >= 3;
                var y = rows[i % 3];
                var x = right ? width - 34f - BarWidth : 34f;
                var anchor = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                var row = Box(canvas, stat.ToString(), new Rect(x, y, BarWidth, 96f));
                var label = Text(row, "Label", labels, 28, anchor, Label, new Rect(0f, -2f, BarWidth, 30f));
                label.text = HangarStats.Label(stat);
                var number = Text(row, "Number", numbers, 36, anchor, Amber, new Rect(0f, 24f, BarWidth, 48f));
                bars[i] = Bar(row, number, new Vector2(0f, 76f));
                var barEnd = right ? new Vector2(x - 10f, y + 80f) : new Vector2(x + BarWidth + 10f, y + 80f);
                var hull = hullPoints[i % 3];
                if (right) hull.x = width - hull.x;
                Segment(canvas, stat + "Leader", barEnd, hull, 2f, Line);
            }

            var glyphs = new Object[2];
            var names = new Object[2];
            var hardpoints = new[] { new Vector2(338f, 410f), new Vector2(402f, 410f) };
            for (var i = 0; i < 2; i++)
            {
                var centre = slots[i + 1].rect.center.x;
                var glyph = Box(canvas, slots[i + 1].name + "Glyph", new Rect(centre - 32f, 468f, 64f, 64f)).gameObject.AddComponent<Image>();
                glyph.color = Color.white;
                glyph.raycastTarget = false;
                glyph.preserveAspect = true;
                glyphs[i] = glyph;
                names[i] = Text(canvas, slots[i + 1].name + "Name", labels, 28, TextAnchor.MiddleCenter, Label,
                    new Rect(centre - 80f, 540f, 160f, 30f));
                Segment(canvas, slots[i + 1].name + "Leader", hardpoints[i], new Vector2(centre, 462f), 2f, Line);
            }

            var silhouette = Box(canvas, "Silhouette", new Rect(250f, 170f, 240f, 240f)).gameObject.AddComponent<RawImage>();
            silhouette.raycastTarget = false;
            foreach (var bracket in brackets) ((GameObject)bracket).transform.SetAsLastSibling();

            var keystoneName = Text(canvas, "KeystoneName", labels, 28, TextAnchor.MiddleCenter, Label, new Rect(560f, 418f, 160f, 30f));
            var keystoneIcon = Box(canvas, "KeystoneIcon", new Rect(604f, 452f, 72f, 72f)).gameObject.AddComponent<Image>();
            keystoneIcon.color = Amber;
            keystoneIcon.raycastTarget = false;
            keystoneIcon.preserveAspect = true;
            var keystoneCooldown = Text(canvas, "KeystoneCooldown", numbers, 36, TextAnchor.MiddleCenter, Amber, new Rect(560f, 530f, 160f, 48f));
            Segment(canvas, "KeystoneLeader", new Vector2(430f, 396f), new Vector2(604f, 488f), 2f, Line);

            view.FindProperty("title").objectReferenceValue = title;
            view.FindProperty("lockTag").objectReferenceValue = lockTag;
            view.FindProperty("silhouette").objectReferenceValue = silhouette;
            Assign(view.FindProperty("bars"), bars);
            Assign(view.FindProperty("buttons"), buttons);
            Assign(view.FindProperty("brackets"), brackets);
            Assign(view.FindProperty("glyphs"), glyphs);
            Assign(view.FindProperty("names"), names);
            view.FindProperty("keystoneIcon").objectReferenceValue = keystoneIcon;
            view.FindProperty("keystoneName").objectReferenceValue = keystoneName;
            view.FindProperty("keystoneCooldown").objectReferenceValue = keystoneCooldown;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LayoutDetail(RectTransform canvas, float width, out Text title, out Text body)
        {
            title = Text(canvas, "Title", Font(TitleFontPath), 30, TextAnchor.MiddleLeft, Amber, new Rect(24f, 10f, width - 48f, 40f));
            body = Text(canvas, "Body", Font(LabelFontPath), 26, TextAnchor.UpperLeft, Label, new Rect(24f, 54f, width - 48f, 80f));
            body.lineSpacing = 1.05f;
        }

        private static HangarStatBar Bar(RectTransform row, Text number, Vector2 at)
        {
            var bar = Box(row, "Bar", new Rect(at.x, at.y, BarWidth, BarHeight));
            var segments = new Object[Segments];
            for (var i = 0; i < Segments; i++)
            {
                var segment = Box(bar, "Segment", new Rect(i * (SegmentWidth + SegmentGap), 0f, SegmentWidth, BarHeight))
                    .gameObject.AddComponent<Image>();
                segment.color = Unlit;
                segment.raycastTarget = false;
                segments[i] = segment;
            }
            var component = bar.gameObject.AddComponent<HangarStatBar>();
            var data = new SerializedObject(component);
            data.FindProperty("number").objectReferenceValue = number;
            Assign(data.FindProperty("segments"), segments);
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static GameObject Bracket(RectTransform parent, string name, Rect rect)
        {
            const float arm = 22f;
            const float thickness = 3f;
            var root = Box(parent, name, rect);
            foreach (var (cx, cy) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                var x = cx * (rect.width - thickness);
                var y = cy * (rect.height - thickness);
                Stroke(root, "Arm", new Rect(cx * (rect.width - arm), y, arm, thickness));
                Stroke(root, "Arm", new Rect(x, cy * (rect.height - arm), thickness, arm));
            }
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static void Stroke(RectTransform parent, string name, Rect rect)
        {
            var image = Box(parent, name, rect).gameObject.AddComponent<Image>();
            image.color = Amber;
            image.raycastTarget = false;
        }

        private static void Segment(RectTransform parent, string name, Vector2 from, Vector2 to, float thickness, Color color)
        {
            var delta = to - from;
            var length = delta.magnitude;
            var line = Box(parent, name, new Rect(from.x, from.y - thickness * .5f, length, thickness));
            line.pivot = new Vector2(0f, .5f);
            line.anchoredPosition = new Vector2(from.x, -from.y);
            line.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = line.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Text Text(RectTransform parent, string name, Font font, int size, TextAnchor alignment, Color color, Rect rect)
        {
            var text = Box(parent, name, rect).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Top-left origin, y down: the grid the screen design was drawn on.
        private static RectTransform Box(RectTransform parent, string name, Rect rect)
        {
            var box = new GameObject(name, typeof(RectTransform));
            box.transform.SetParent(parent, false);
            var transform = box.GetComponent<RectTransform>();
            transform.anchorMin = transform.anchorMax = new Vector2(0f, 1f);
            transform.pivot = new Vector2(0f, 1f);
            transform.anchoredPosition = new Vector2(rect.x, -rect.y);
            transform.sizeDelta = rect.size;
            return transform;
        }

        private static Font Font(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (!font) throw new System.InvalidOperationException($"Hangar displays need the font at {path}.");
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

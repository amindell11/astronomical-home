using UI.Hangar.Selection;
using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// Builds the console's display rigs. Each layout is authored at a fixed design size,
    /// top-left origin, and fitted uniformly into that glass's content rectangle.
    /// </summary>
    public static class HangarDisplayBuilder
    {
        private const string LabelFontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";
        private const string TitleFontPath = "Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf";
        private const string NumberFontPath = "Assets/Visuals/Ui/Fonts/IBMPlexMono.ttf";
        private const int MainDesignWidth = 700;
        private const int MainDesignHeight = 495;
        private const int DetailDesignWidth = 594;
        private const int DetailDesignHeight = 142;
        private const int WeaponDesignHeight = 296;
        private const int Segments = 12;
        private const float SegmentWidth = 9f;
        private const float SegmentGap = 3f;
        private const float BarHeight = 8f;
        private const float BarWidth = Segments * SegmentWidth + (Segments - 1) * SegmentGap;
        private const float ArrowWidth = 48f;
        private const float ArrowHeight = 56f;
        private const string HintKeys = "\u2039 \u203a  <color=#9FB0BF>or</color>  A / D";
        private const string HintVerb = "<color=#9FB0BF>to change</color>";

        private static readonly Color Amber = new(1f, .80f, .50f, 1f);
        private static readonly Color Ink = new(.84f, .92f, 1f, 1f);
        private static readonly Color Label = new(.62f, .70f, .78f, 1f);
        private static readonly Color Description = new(.88f, .92f, .97f, 1f);
        private static readonly Color LeaderInk = new(.45f, .58f, .68f, 1f);
        private static readonly Color Locked = new(1f, .42f, .32f, 1f);
        private static readonly Color TagFill = new(.08f, .14f, .24f, 1f);
        private static readonly Color Clear = new(0f, 0f, 0f, 0f);

        public static HangarMainDisplayView BuildMain(Transform parent, Vector3 localPosition, Vector2Int resolution,
            Rect content, Object feature)
        {
            var display = Rig(parent, "MainDisplay", localPosition, resolution, feature, out var canvas);
            var view = display.gameObject.AddComponent<HangarMainDisplayView>();
            LayoutMain(Fit(canvas, resolution, content, MainDesignWidth, MainDesignHeight), new SerializedObject(view));
            return view;
        }

        public static HangarDetailDisplayView BuildDetail(Transform parent, Vector3 localPosition, Vector2Int resolution,
            Rect content, Object feature)
        {
            var display = Rig(parent, "DetailDisplay", localPosition, resolution, feature, out var canvas);
            LayoutDetail(Fit(canvas, resolution, content, DetailDesignWidth, DetailDesignHeight), out var title, out var body);
            var view = display.gameObject.AddComponent<HangarDetailDisplayView>();
            var data = new SerializedObject(view);
            data.FindProperty("title").objectReferenceValue = title;
            data.FindProperty("body").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        public static HangarWeaponDisplayView BuildWeapon(Transform parent, string name, Vector3 localPosition,
            Vector2Int resolution, Rect content, Object feature, HangarSlot slot, string header)
        {
            var display = Rig(parent, name, localPosition, resolution, feature, out var canvas);
            var view = display.gameObject.AddComponent<HangarWeaponDisplayView>();
            // PRIMARY and ORDNANCE share a height but not a width, so the width follows each glass.
            var width = Mathf.RoundToInt(WeaponDesignHeight * content.width * resolution.x / (content.height * resolution.y));
            LayoutWeapon(Fit(canvas, resolution, content, width, WeaponDesignHeight), width,
                new SerializedObject(view), slot, header);
            return view;
        }

        private static RectTransform Fit(RectTransform canvas, Vector2Int resolution, Rect content,
            int designWidth, int designHeight)
        {
            var frameObject = new GameObject("Content", typeof(RectTransform));
            frameObject.transform.SetParent(canvas, false);
            var frame = frameObject.GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(content.xMin, content.yMin);
            frame.anchorMax = new Vector2(content.xMax, content.yMax);
            frame.offsetMin = frame.offsetMax = Vector2.zero;

            var layoutObject = new GameObject("Layout", typeof(RectTransform));
            layoutObject.transform.SetParent(frame, false);
            var layout = layoutObject.GetComponent<RectTransform>();
            layout.anchorMin = layout.anchorMax = new Vector2(.5f, .5f);
            layout.pivot = new Vector2(.5f, .5f);
            layout.anchoredPosition = Vector2.zero;
            layout.sizeDelta = new Vector2(designWidth, designHeight);
            var scale = Mathf.Min(content.width * resolution.x / designWidth, content.height * resolution.y / designHeight);
            layout.localScale = new Vector3(scale, scale, scale);
            return layout;
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

        private static void LayoutMain(RectTransform layout, SerializedObject view)
        {
            var width = (float)MainDesignWidth;
            var labels = Font(LabelFontPath);
            var titles = Font(TitleFontPath);
            var numbers = Font(NumberFontPath);

            var shipRect = new Rect(width * .5f - 160f, 4f, 320f, 376f);
            var shipObject = Box(layout, "Ship", shipRect).gameObject;
            var shipFace = shipObject.AddComponent<Image>();
            shipFace.color = Clear;
            shipFace.raycastTarget = true;
            var shipButton = shipObject.AddComponent<Button>();
            shipButton.targetGraphic = shipFace;
            shipButton.transition = Selectable.Transition.None;
            var shipBracket = Bracket(layout, "ShipBracket", shipRect);

            var title = Text(layout, "Title", titles, 40, TextAnchor.MiddleCenter, Ink, new Rect(0f, 6f, width, 52f));
            var lockTag = Text(layout, "LockTag", labels, 24, TextAnchor.MiddleCenter, Locked, new Rect(0f, 56f, width, 26f));
            lockTag.text = "LOCKED";
            var hint = Text(layout, "Hint", labels, 34, TextAnchor.MiddleCenter, Ink, new Rect(0f, 54f, width, 34f));
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.text = HintKeys + "  " + HintVerb;
            hint.gameObject.SetActive(false);
            lockTag.gameObject.SetActive(false);

            const float silhouetteSize = 280f;
            const float silhouetteY = 88f;
            var silhouetteX = width * .5f - silhouetteSize * .5f;
            var bars = new Object[HangarStats.Count];
            var rows = new[] { 84f, 180f, 276f };
            var hullPoints = new[] { new Vector2(.21f, .23f), new Vector2(.175f, .51f), new Vector2(.21f, .775f) };
            for (var i = 0; i < HangarStats.Count; i++)
            {
                var stat = (HangarStat)i;
                var right = i >= 3;
                var y = rows[i % 3];
                var x = right ? width - 28f - BarWidth : 28f;
                var anchor = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                var row = Box(layout, stat.ToString(), new Rect(x, y, BarWidth, 96f));
                var label = Text(row, "Label", labels, 28, anchor, Label, new Rect(0f, -2f, BarWidth, 30f));
                label.text = stat.ToString();
                var number = Text(row, "Number", numbers, 36, anchor, Ink, new Rect(0f, 24f, BarWidth, 48f));
                bars[i] = Bar(row, number, new Vector2(0f, 76f));
                var barEnd = right ? new Vector2(x - 10f, y + 80f) : new Vector2(x + BarWidth + 10f, y + 80f);
                var hull = new Vector2(silhouetteX + hullPoints[i % 3].x * silhouetteSize,
                    silhouetteY + hullPoints[i % 3].y * silhouetteSize);
                if (right) hull.x = width - hull.x;
                Leader(layout, stat + "Leader", barEnd, hull);
            }

            var silhouette = Box(layout, "Silhouette", new Rect(silhouetteX, silhouetteY, silhouetteSize, silhouetteSize))
                .gameObject.AddComponent<RawImage>();
            silhouette.raycastTarget = false;

            const float ruleInset = 28f;
            const float ruleTop = 378f;
            const float ruleBottom = 468f;
            const float ruleThickness = 2f;
            const float keystoneIconX = 48f;
            const float keystoneIconY = 386f;
            const float keystoneIconSize = 72f;
            const float keystoneNameX = 140f;
            const float keystoneNameY = 396f;
            const float keystoneNameWidth = 360f;
            const float keystoneNameHeight = 52f;
            const int keystoneNameSize = 34;
            const float keystoneCooldownWidth = 200f;
            const float keystoneCooldownY = 398f;
            const float keystoneCooldownHeight = 48f;
            const int keystoneCooldownSize = 36;
            Line(layout, "KeystoneTop", new Vector2(ruleInset, ruleTop), new Vector2(width - ruleInset, ruleTop),
                ruleThickness, LeaderInk);
            Line(layout, "KeystoneBottom", new Vector2(ruleInset, ruleBottom), new Vector2(width - ruleInset, ruleBottom),
                ruleThickness, LeaderInk);
            var keystoneIcon = Box(layout, "KeystoneIcon",
                new Rect(keystoneIconX, keystoneIconY, keystoneIconSize, keystoneIconSize)).gameObject.AddComponent<Image>();
            keystoneIcon.color = Ink;
            keystoneIcon.raycastTarget = false;
            keystoneIcon.preserveAspect = true;
            var keystoneName = Text(layout, "KeystoneName", labels, keystoneNameSize, TextAnchor.MiddleLeft, Label,
                new Rect(keystoneNameX, keystoneNameY, keystoneNameWidth, keystoneNameHeight));
            var keystoneCooldown = Text(layout, "KeystoneCooldown", numbers, keystoneCooldownSize, TextAnchor.MiddleRight,
                Ink, new Rect(width - ruleInset - keystoneCooldownWidth, keystoneCooldownY, keystoneCooldownWidth,
                    keystoneCooldownHeight));

            shipBracket.transform.SetAsLastSibling();
            var previousArrow = Arrow(layout, "ShipPrevious",
                new Rect(width * .5f - 240f, 4f, ArrowWidth, ArrowHeight), -1, false);
            var nextArrow = Arrow(layout, "ShipNext",
                new Rect(width * .5f + 192f, 4f, ArrowWidth, ArrowHeight), 1, false);

            view.FindProperty("title").objectReferenceValue = title;
            view.FindProperty("lockTag").objectReferenceValue = lockTag;
            view.FindProperty("silhouette").objectReferenceValue = silhouette;
            Assign(view.FindProperty("bars"), bars);
            view.FindProperty("shipButton").objectReferenceValue = shipButton;
            view.FindProperty("shipBracket").objectReferenceValue = shipBracket;
            view.FindProperty("hint").objectReferenceValue = hint.gameObject;
            view.FindProperty("previousArrow").objectReferenceValue = previousArrow;
            view.FindProperty("nextArrow").objectReferenceValue = nextArrow;
            view.FindProperty("keystoneIcon").objectReferenceValue = keystoneIcon;
            view.FindProperty("keystoneName").objectReferenceValue = keystoneName;
            view.FindProperty("keystoneCooldown").objectReferenceValue = keystoneCooldown;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LayoutDetail(RectTransform layout, out Text title, out Text body)
        {
            var width = (float)DetailDesignWidth;
            title = Text(layout, "Title", Font(TitleFontPath), 30, TextAnchor.MiddleLeft, Ink, new Rect(18f, 12f, width - 36f, 40f));
            body = Text(layout, "Body", Font(LabelFontPath), 26, TextAnchor.UpperLeft, Label, new Rect(18f, 54f, width - 36f, 72f));
            body.lineSpacing = 1.05f;
        }

        private static void LayoutWeapon(RectTransform layout, float width, SerializedObject view, HangarSlot slot,
            string header)
        {
            var height = (float)WeaponDesignHeight;
            var labels = Font(LabelFontPath);
            var titles = Font(TitleFontPath);
            var numbers = Font(NumberFontPath);

            const float margin = 24f;
            const float headerX = 28f;
            const float headerY = 20f;
            const float headerWidth = 140f;
            const float headerHeight = 30f;
            const int headerSize = 24;
            const float titleHeight = 56f;
            const float titleY = 10f;
            const int titleSize = 38;
            const float arrowWidth = 48f;
            const float arrowHeight = 56f;
            const float arrowY = 10f;
            const float iconX = 28f;
            const float iconY = 84f;
            const float iconSize = 116f;
            const float dividerX = 162f;
            const float dividerTop = 78f;
            const float dividerBottom = 274f;
            const float dividerThickness = 2f;
            const float descriptionX = 180f;
            const float descriptionY = 66f;
            const float descriptionHeight = 118f;
            const int descriptionSize = 30;
            const float descriptionLineSpacing = 1.05f;
            const float tagRowX = 180f;
            const float tagRowFromBottom = 66f;
            const float tagRowHeight = 36f;
            const float tagSpacing = 10f;
            const int tagPadX = 12;
            const int tagPadY = 2;
            const int tagCount = 3;
            const int tagSize = 24;
            const float damageWidth = 170f;
            const float damageFromBottom = 92f;
            const float damageHeight = 56f;
            const int damageSize = 56;
            const float captionWidth = 300f;
            const float captionFromBottom = 38f;
            const float captionHeight = 26f;
            const int captionSize = 24;
            const float damageLabelFromBottom = 112f;
            const float damageLabelHeight = 26f;
            const int damageLabelSize = 22;
            const float bracketInset = 8f;
            var titleLeft = dividerX + arrowWidth;
            var titleRight = width - margin - arrowWidth;

            var selectObject = Box(layout, "Select", new Rect(0f, 0f, width, height)).gameObject;
            var selectFace = selectObject.AddComponent<Image>();
            selectFace.color = Clear;
            selectFace.raycastTarget = true;
            var select = selectObject.AddComponent<Button>();
            select.targetGraphic = selectFace;
            select.transition = Selectable.Transition.None;

            var content = Box(layout, "Content", new Rect(0f, 0f, width, height));
            var group = content.gameObject.AddComponent<CanvasGroup>();

            var headerText = Text(content, "Header", labels, headerSize, TextAnchor.UpperLeft, Label,
                new Rect(headerX, headerY, headerWidth, headerHeight));
            headerText.text = header;
            var hint = Text(content, "Hint", labels, 23, TextAnchor.UpperLeft, Ink,
                new Rect(headerX, headerY + 24f, dividerX - headerX, 44f));
            hint.lineSpacing = .9f;
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.text = HintKeys + "\n" + HintVerb;
            hint.gameObject.SetActive(false);
            var title = Text(content, "Title", titles, titleSize, TextAnchor.MiddleCenter, Ink,
                new Rect(titleLeft, titleY, titleRight - titleLeft, titleHeight));
            var previous = Arrow(content, "Previous", new Rect(dividerX, arrowY, arrowWidth, arrowHeight), -1, true);
            var next = Arrow(content, "Next", new Rect(titleRight, arrowY, arrowWidth, arrowHeight), 1, true);
            var icon = Box(content, "Icon", new Rect(iconX, iconY, iconSize, iconSize)).gameObject.AddComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Line(content, "Divider", new Vector2(dividerX, dividerTop), new Vector2(dividerX, dividerBottom),
                dividerThickness, LeaderInk);
            var description = Text(content, "Description", labels, descriptionSize, TextAnchor.UpperLeft, Description,
                new Rect(descriptionX, descriptionY, width - descriptionX - margin, descriptionHeight));
            description.lineSpacing = descriptionLineSpacing;

            var tagRow = Box(content, "Tags",
                new Rect(tagRowX, height - tagRowFromBottom, width - tagRowX - margin - damageWidth, tagRowHeight));
            var rowLayout = tagRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = tagSpacing;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var tags = new Object[tagCount];
            var tagLabels = new Object[tagCount];
            for (var i = 0; i < tagCount; i++)
            {
                var tag = Box(tagRow, "Tag", new Rect(0f, 0f, 0f, 0f));
                var tagImage = tag.gameObject.AddComponent<Image>();
                tagImage.color = TagFill;
                tagImage.raycastTarget = false;
                var tagLayout = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
                tagLayout.padding = new RectOffset(tagPadX, tagPadX, tagPadY, tagPadY);
                tagLayout.childControlWidth = true;
                tagLayout.childControlHeight = true;
                tagLayout.childForceExpandWidth = false;
                tagLayout.childForceExpandHeight = false;
                var label = Text(tag, "Label", labels, tagSize, TextAnchor.MiddleCenter, Ink, new Rect(0f, 0f, 0f, 0f));
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                tag.gameObject.SetActive(false);
                tags[i] = tag.gameObject;
                tagLabels[i] = label;
            }

            var damageLabel = Text(content, "DamageLabel", labels, damageLabelSize, TextAnchor.MiddleRight, Label,
                new Rect(width - margin - damageWidth, height - damageLabelFromBottom, damageWidth, damageLabelHeight));
            damageLabel.text = "DAMAGE";
            var damage = Text(content, "Damage", numbers, damageSize, TextAnchor.MiddleRight, Ink,
                new Rect(width - margin - damageWidth, height - damageFromBottom, damageWidth, damageHeight));
            var damageCaption = Text(content, "DamageCaption", labels, captionSize, TextAnchor.UpperRight, Label,
                new Rect(width - margin - captionWidth, height - captionFromBottom, captionWidth, captionHeight));
            var bracket = Bracket(layout, "Bracket",
                new Rect(bracketInset, bracketInset, width - bracketInset * 2f, height - bracketInset * 2f));

            view.FindProperty("slot").enumValueIndex = (int)slot;
            view.FindProperty("header").objectReferenceValue = headerText;
            view.FindProperty("title").objectReferenceValue = title;
            view.FindProperty("icon").objectReferenceValue = icon;
            view.FindProperty("description").objectReferenceValue = description;
            Assign(view.FindProperty("tags"), tags);
            Assign(view.FindProperty("tagLabels"), tagLabels);
            view.FindProperty("damage").objectReferenceValue = damage;
            view.FindProperty("damageCaption").objectReferenceValue = damageCaption;
            view.FindProperty("bracket").objectReferenceValue = bracket;
            view.FindProperty("hint").objectReferenceValue = hint.gameObject;
            view.FindProperty("select").objectReferenceValue = select;
            view.FindProperty("previous").objectReferenceValue = previous;
            view.FindProperty("next").objectReferenceValue = next;
            view.FindProperty("content").objectReferenceValue = group;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HangarStatBar Bar(RectTransform row, Text number, Vector2 at)
        {
            var bar = Box(row, "Bar", new Rect(at.x, at.y, BarWidth, BarHeight));
            var segments = new Object[Segments];
            for (var i = 0; i < Segments; i++)
            {
                var segment = Box(bar, "Segment", new Rect(i * (SegmentWidth + SegmentGap), 0f, SegmentWidth, BarHeight))
                    .gameObject.AddComponent<Image>();
                segment.color = HangarStatBar.Unlit;
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

        // A chevron on a hit box; the box lights on hover and press, the chevron stays lit.
        private static Button Arrow(RectTransform canvas, string name, Rect rect, int direction, bool shown)
        {
            var root = Box(canvas, name, rect);
            var fill = root.gameObject.AddComponent<Image>();
            fill.color = Ink;
            fill.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 1f, 1f, 0f),
                highlightedColor = new Color(1f, 1f, 1f, .3f),
                pressedColor = new Color(1f, 1f, 1f, .7f),
                selectedColor = new Color(1f, 1f, 1f, 0f),
                disabledColor = new Color(1f, 1f, 1f, 0f),
                colorMultiplier = 1f,
                fadeDuration = .05f
            };
            var tip = new Vector2(direction < 0 ? 14f : rect.width - 14f, rect.height * .5f);
            var tail = direction < 0 ? rect.width - 14f : 14f;
            Line(root, "Arm", tip, new Vector2(tail, rect.height * .5f - 18f), 4f, Ink);
            Line(root, "Arm", tip, new Vector2(tail, rect.height * .5f + 18f), 4f, Ink);
            root.gameObject.SetActive(shown);
            return button;
        }

        private static void Stroke(RectTransform parent, string name, Rect rect)
        {
            var image = Box(parent, name, rect).gameObject.AddComponent<Image>();
            image.color = Amber;
            image.raycastTarget = false;
        }

        private static void Leader(RectTransform parent, string name, Vector2 from, Vector2 to) =>
            Line(parent, name, from, to, 2f, LeaderInk);

        private static void Line(RectTransform parent, string name, Vector2 from, Vector2 to, float thickness, Color color)
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

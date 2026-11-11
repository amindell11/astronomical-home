using Substrate.Sessions;
using UnityEditor;
using UnityEngine;

namespace Game.Preview.Fixtures
{
    [CustomPropertyDrawer(typeof(PreviewFixture))]
    internal sealed class PreviewFixtureDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 16f;

        private static readonly (PreviewFixtureOwn flag, string label, string[] names)[] Fields =
        {
            (PreviewFixtureOwn.Faction, "Faction", new[] { nameof(SectorEntry.faction) }),
            (PreviewFixtureOwn.Elite, "Elite", new[] { nameof(SectorEntry.elite) }),
            (PreviewFixtureOwn.Waves, "Waves", new[] { nameof(SectorEntry.phases) }),
            (PreviewFixtureOwn.Patrols, "Patrols", new[] { nameof(SectorEntry.patrols) }),
            (PreviewFixtureOwn.Field, "Field", new[] { nameof(SectorEntry.field) }),
            (PreviewFixtureOwn.Locale, "Locale", new[] { nameof(SectorEntry.locale), nameof(SectorEntry.random) }),
        };

        private static float Line => EditorGUIUtility.singleLineHeight;

        private static float Gap => EditorGUIUtility.standardVerticalSpacing;

        private static SerializedObject cachedRun;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var link = property.FindPropertyRelative(nameof(PreviewFixture.link)).intValue;
            var own = (PreviewFixtureOwn)property.FindPropertyRelative(nameof(PreviewFixture.own)).intValue;
            var values = property.FindPropertyRelative(nameof(PreviewFixture.values));
            var row = LinkedRow(link);
            var height = Line + Gap + Line;
            foreach (var field in Fields)
                height += Gap + BlockHeight(link, own, field, values, row);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var indent = EditorGUI.indentLevel;
            var rect = EditorGUI.IndentedRect(position);
            EditorGUI.indentLevel = 0;
            var link = property.FindPropertyRelative(nameof(PreviewFixture.link));
            var own = property.FindPropertyRelative(nameof(PreviewFixture.own));
            var values = property.FindPropertyRelative(nameof(PreviewFixture.values));
            var row = LinkedRow(link.intValue);
            try
            {
                var line = Take(ref rect, Line);
                EditorGUI.PropertyField(line, property.FindPropertyRelative(nameof(PreviewFixture.layout)));
                rect.y += Gap;
                line = Take(ref rect, Line);
                var linkBefore = link.intValue;
                DrawSector(line, link);
                if (link.intValue != linkBefore)
                    row = LinkedRow(link.intValue);
                var flags = (PreviewFixtureOwn)own.intValue;
                foreach (var field in Fields)
                {
                    rect.y += Gap;
                    var fromRun = link.intValue != PreviewFixture.Unlinked && (flags & field.flag) == 0;
                    var height = BlockHeight(link.intValue, flags, field, values, row);
                    line = Take(ref rect, height);
                    if (link.intValue != PreviewFixture.Unlinked)
                    {
                        var toggle = new Rect(line.x, line.y, ToggleWidth, Line);
                        var ticked = (flags & field.flag) != 0;
                        var next = EditorGUI.Toggle(toggle, ticked);
                        if (next != ticked)
                        {
                            flags = next ? flags | field.flag : flags & ~field.flag;
                            own.intValue = (int)flags;
                            fromRun = !next;
                        }
                        line.x += ToggleWidth + Gap;
                        line.width -= ToggleWidth + Gap;
                    }
                    if (fromRun && row == null)
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.LabelField(line, $"{field.label} from Sector {link.intValue}");
                        EditorGUI.EndDisabledGroup();
                        continue;
                    }
                    var source = fromRun ? row : values;
                    var caption = fromRun ? $"{field.label} from Sector {link.intValue}" : field.label;
                    DrawBlock(line, source, field.names, caption, fromRun);
                }
            }
            finally
            {
                EditorGUI.indentLevel = indent;
                EditorGUI.EndProperty();
            }
        }

        private static float BlockHeight(int link, PreviewFixtureOwn own,
            (PreviewFixtureOwn flag, string label, string[] names) field, SerializedProperty values,
            SerializedProperty row)
        {
            var fromRun = link != PreviewFixture.Unlinked && (own & field.flag) == 0;
            if (fromRun && row == null) return Line;
            var source = fromRun ? row : values;
            var height = 0f;
            for (var i = 0; i < field.names.Length; i++)
            {
                if (i > 0) height += Gap;
                height += EditorGUI.GetPropertyHeight(source.FindPropertyRelative(field.names[i]), true);
            }
            return height;
        }

        private static void DrawBlock(Rect rect, SerializedProperty source, string[] names, string caption, bool disabled)
        {
            EditorGUI.BeginDisabledGroup(disabled);
            var y = rect.y;
            for (var i = 0; i < names.Length; i++)
            {
                var property = source.FindPropertyRelative(names[i]);
                var height = EditorGUI.GetPropertyHeight(property, true);
                var line = new Rect(rect.x, y, rect.width, height);
                var text = i == 0 ? caption : ObjectNames.NicifyVariableName(names[i]);
                EditorGUI.PropertyField(line, property, new GUIContent(text, property.tooltip), true);
                y += height + Gap;
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawSector(Rect rect, SerializedProperty link)
        {
            var count = GameRun().Sectors.Count;
            var stored = link.intValue;
            var selected = stored < PreviewFixture.Unlinked ? PreviewFixture.Unlinked : stored;
            var labels = new string[Mathf.Max(count, selected) + 1];
            labels[0] = "None";
            for (var i = 1; i < labels.Length; i++) labels[i] = $"Sector {i}";
            var picked = EditorGUI.Popup(rect, "Sector", selected, labels);
            if (picked != selected) link.intValue = picked;
        }

        private static SerializedProperty LinkedRow(int link)
        {
            if (link == PreviewFixture.Unlinked) return null;
            var run = GameRun();
            var index = link - 1;
            if (index < 0 || index >= run.Sectors.Count) return null;
            if (cachedRun == null || cachedRun.targetObject != run)
            {
                cachedRun?.Dispose();
                cachedRun = new SerializedObject(run);
            }
            else
                cachedRun.Update();
            return cachedRun.FindProperty("sectors").GetArrayElementAtIndex(index);
        }

        private static SectorRun GameRun() =>
            AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath).sessionProfile.run;

        private static Rect Take(ref Rect rect, float height)
        {
            var line = new Rect(rect.x, rect.y, rect.width, height);
            rect.y += height;
            return line;
        }
    }
}

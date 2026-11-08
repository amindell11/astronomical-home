using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Substrate.Sessions
{
    [CustomEditor(typeof(SectorRun))]
    public sealed class SectorRunEditor : UnityEditor.Editor
    {
        private const float IndexWidth = 24f;
        private const float RandomWidth = 72f;
        private const float PatrolsWidth = 48f;
        private const float Gap = 6f;
        private const int WideColumns = 5;
        private static readonly string[] Unfolded = { nameof(SectorEntry.roster), nameof(SectorEntry.phases) };

        private ReorderableList table;
        private SerializedProperty localePool;

        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private void OnEnable()
        {
            localePool = serializedObject.FindProperty("localePool");
            table = new ReorderableList(serializedObject, serializedObject.FindProperty("sectors"), true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    var (index, sector, locale, faction, elite, patrols, field) = Columns(rect);
                    EditorGUI.LabelField(index, "#");
                    EditorGUI.LabelField(sector, "Sector");
                    EditorGUI.LabelField(locale, "Locale");
                    EditorGUI.LabelField(faction, "Faction");
                    EditorGUI.LabelField(elite, "Elite");
                    EditorGUI.LabelField(patrols, "Patrols");
                    EditorGUI.LabelField(field, "Field");
                },
                elementHeightCallback = i => RowHeight(table.serializedProperty.GetArrayElementAtIndex(i)),
                drawElementCallback = (rect, i, _, _) => DrawRow(rect, i, table.serializedProperty.GetArrayElementAtIndex(i))
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            table.DoLayoutList();
            EditorGUILayout.PropertyField(localePool, true);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawRow(Rect rect, int i, SerializedProperty row)
        {
            var line = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            var (index, sector, locale, faction, elite, patrols, field) = Columns(line);
            EditorGUI.LabelField(index, (i + 1).ToString());
            Draw(sector, row, nameof(SectorEntry.prefab));
            var random = row.FindPropertyRelative(nameof(SectorEntry.random));
            random.boolValue = EditorGUI.ToggleLeft(new Rect(locale.x, locale.y, RandomWidth, locale.height),
                "Random", random.boolValue);
            if (!random.boolValue)
                Draw(new Rect(locale.x + RandomWidth, locale.y, locale.width - RandomWidth, locale.height), row,
                    nameof(SectorEntry.locale));
            Draw(faction, row, nameof(SectorEntry.faction));
            Draw(elite, row, nameof(SectorEntry.elite));
            Draw(patrols, row, nameof(SectorEntry.patrols));
            Draw(field, row, nameof(SectorEntry.field));

            line.y += Line;
            row.isExpanded = EditorGUI.Foldout(line, row.isExpanded, "Roster and phases", true);
            if (!row.isExpanded) return;
            EditorGUI.indentLevel++;
            foreach (var name in Unfolded)
            {
                var property = row.FindPropertyRelative(name);
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(property, true);
                EditorGUI.PropertyField(line, property, true);
            }
            EditorGUI.indentLevel--;
        }

        private static float RowHeight(SerializedProperty row)
        {
            var height = 2f * Line + 4f;
            if (!row.isExpanded) return height;
            foreach (var name in Unfolded)
                height += EditorGUI.GetPropertyHeight(row.FindPropertyRelative(name), true) +
                          EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        private static void Draw(Rect rect, SerializedProperty row, string name) =>
            EditorGUI.PropertyField(rect, row.FindPropertyRelative(name), GUIContent.none);

        private static (Rect index, Rect sector, Rect locale, Rect faction, Rect elite, Rect patrols, Rect field)
            Columns(Rect rect)
        {
            var column = (rect.width - IndexWidth - PatrolsWidth - (WideColumns + 1) * Gap) / WideColumns;
            var x = rect.x;
            Rect Next(float width)
            {
                var next = new Rect(x, rect.y, width, rect.height);
                x += width + Gap;
                return next;
            }
            return (Next(IndexWidth), Next(column), Next(column), Next(column), Next(column), Next(PatrolsWidth),
                Next(column));
        }
    }
}

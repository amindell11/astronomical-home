using Game.Preview.Sectors;
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
        private const int WideColumns = 4;
        private static readonly string[] Unfolded = { nameof(SectorEntry.phases) };

        private ReorderableList table;
        private SerializedProperty localePool;

        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private void OnEnable()
        {
            localePool = serializedObject.FindProperty("localePool");
            table = new ReorderableList(serializedObject, serializedObject.FindProperty("sectors"), true, true, true, true)
            {
                headerHeight = 2f * Line + 2f,
                drawHeaderCallback = rect =>
                {
                    var line = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                    var (index, sector, faction, elite, patrols, armories, field) = Columns(line);
                    EditorGUI.LabelField(index, "#");
                    EditorGUI.LabelField(sector, "Sector");
                    EditorGUI.LabelField(faction, "Faction");
                    EditorGUI.LabelField(elite, "Elite");
                    EditorGUI.LabelField(patrols, "Patrols");
                    EditorGUI.LabelField(armories, "Armories");
                    EditorGUI.LabelField(field, "Field");
                    line.y += Line;
                    var (locale, search, repairs, mods) = LocaleColumns(line);
                    EditorGUI.LabelField(locale, "Locale");
                    EditorGUI.LabelField(search, "Search");
                    EditorGUI.LabelField(repairs, "Repairs");
                    EditorGUI.LabelField(mods, "Mods");
                },
                elementHeightCallback = i => RowHeight(table.serializedProperty.GetArrayElementAtIndex(i)),
                drawElementCallback = (rect, i, _, _) => DrawRow(rect, i, table.serializedProperty.GetArrayElementAtIndex(i))
            };
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open in Sectors window"))
                SectorsWindow.Open();
            serializedObject.Update();
            table.DoLayoutList();
            EditorGUILayout.PropertyField(localePool, true);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawRow(Rect rect, int i, SerializedProperty row)
        {
            var line = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            var (index, sector, faction, elite, patrols, armories, field) = Columns(line);
            EditorGUI.LabelField(index, (i + 1).ToString());
            Draw(sector, row, nameof(SectorEntry.prefab));
            Draw(faction, row, nameof(SectorEntry.faction));
            Draw(elite, row, nameof(SectorEntry.elite));
            Draw(patrols, row, nameof(SectorEntry.patrols));
            Draw(armories, row, nameof(SectorEntry.armories));
            Draw(field, row, nameof(SectorEntry.field));

            line.y += Line;
            var (locale, search, repairs, mods) = LocaleColumns(line);
            var random = row.FindPropertyRelative(nameof(SectorEntry.random));
            random.boolValue = EditorGUI.ToggleLeft(new Rect(locale.x, locale.y, RandomWidth, locale.height),
                "Random", random.boolValue);
            if (!random.boolValue)
                Draw(new Rect(locale.x + RandomWidth, locale.y, locale.width - RandomWidth, locale.height), row,
                    nameof(SectorEntry.locale));
            Draw(search, row, nameof(SectorEntry.searchSeconds));
            Draw(repairs, row, nameof(SectorEntry.repairStations));
            Draw(mods, row, nameof(SectorEntry.modStations));

            line.y += Line;
            row.isExpanded = EditorGUI.Foldout(line, row.isExpanded, "Phases and waves", true);
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
            var height = 3f * Line + 4f;
            if (!row.isExpanded) return height;
            foreach (var name in Unfolded)
                height += EditorGUI.GetPropertyHeight(row.FindPropertyRelative(name), true) +
                          EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        private static void Draw(Rect rect, SerializedProperty row, string name) =>
            EditorGUI.PropertyField(rect, row.FindPropertyRelative(name), GUIContent.none);

        // The locale controls get a line of their own, so a docked Inspector never squeezes the Random toggle.
        private static Rect LocaleLine(Rect line) =>
            new(line.x + IndexWidth + Gap, line.y, line.width - IndexWidth - Gap, line.height);

        private static (Rect locale, Rect search, Rect repairs, Rect mods) LocaleColumns(Rect line)
        {
            var full = LocaleLine(line);
            var locale = new Rect(full.x, full.y, full.width - 3 * (PatrolsWidth + Gap), full.height);
            var x = locale.xMax;
            Rect Next()
            {
                x += Gap;
                var next = new Rect(x, full.y, PatrolsWidth, full.height);
                x += PatrolsWidth;
                return next;
            }
            return (locale, Next(), Next(), Next());
        }

        private static (Rect index, Rect sector, Rect faction, Rect elite, Rect patrols, Rect armories, Rect field) Columns(Rect rect)
        {
            var column = (rect.width - IndexWidth - 2f * PatrolsWidth - (WideColumns + 1) * Gap) / WideColumns;
            var x = rect.x;
            Rect Next(float width)
            {
                var next = new Rect(x, rect.y, width, rect.height);
                x += width + Gap;
                return next;
            }
            return (Next(IndexWidth), Next(column), Next(column), Next(column), Next(PatrolsWidth), Next(PatrolsWidth), Next(column));
        }
    }
}

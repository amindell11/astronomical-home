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
        private const float Gap = 6f;

        private ReorderableList table;
        private SerializedProperty localePool;

        private void OnEnable()
        {
            localePool = serializedObject.FindProperty("localePool");
            table = new ReorderableList(serializedObject, serializedObject.FindProperty("sectors"), true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    var (index, sector, locale) = Columns(rect);
                    EditorGUI.LabelField(index, "#");
                    EditorGUI.LabelField(sector, "Sector");
                    EditorGUI.LabelField(locale, "Locale");
                },
                drawElementCallback = (rect, i, _, _) =>
                {
                    var row = table.serializedProperty.GetArrayElementAtIndex(i);
                    rect.y += 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    var (index, sector, locale) = Columns(rect);
                    EditorGUI.LabelField(index, (i + 1).ToString());
                    EditorGUI.PropertyField(sector, row.FindPropertyRelative(nameof(SectorEntry.prefab)), GUIContent.none);
                    var random = row.FindPropertyRelative(nameof(SectorEntry.random));
                    random.boolValue = EditorGUI.ToggleLeft(new Rect(locale.x, locale.y, RandomWidth, locale.height),
                        "Random", random.boolValue);
                    if (random.boolValue) return;
                    var named = new Rect(locale.x + RandomWidth, locale.y, locale.width - RandomWidth, locale.height);
                    EditorGUI.PropertyField(named, row.FindPropertyRelative(nameof(SectorEntry.locale)), GUIContent.none);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            table.DoLayoutList();
            EditorGUILayout.PropertyField(localePool, true);
            serializedObject.ApplyModifiedProperties();
        }

        private static (Rect index, Rect sector, Rect locale) Columns(Rect rect)
        {
            var column = (rect.width - IndexWidth - 2 * Gap) / 2f;
            return (new Rect(rect.x, rect.y, IndexWidth, rect.height),
                new Rect(rect.x + IndexWidth + Gap, rect.y, column, rect.height),
                new Rect(rect.x + IndexWidth + 2 * Gap + column, rect.y, column, rect.height));
        }
    }
}

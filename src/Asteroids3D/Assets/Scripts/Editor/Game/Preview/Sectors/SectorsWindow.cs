using System.Collections.Generic;
using Game.Preview.Fixtures;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;

namespace Game.Preview.Sectors
{
    internal sealed class SectorsWindow : EditorWindow
    {
        private static readonly string[] Timing =
        {
            nameof(SectorPhase.seconds),
            nameof(SectorPhase.cap),
            nameof(SectorPhase.endsAt),
            nameof(SectorPhase.breather),
            nameof(SectorPhase.longestWait),
        };

        private readonly Dictionary<int, SerializedObject> editors = new();
        private readonly HashSet<string> unfolded = new();
        [SerializeField] private int selected;
        [SerializeField] private bool usesOpen = true;
        private SectorPages pages;
        private SerializedObject runObject;
        private SerializedObject fixturesObject;
        private Vector2 stripScroll;
        private Vector2 listScroll;
        private Vector2 pageScroll;

        [MenuItem("Window/Sectors")]
        public static void Open() => GetWindow<SectorsWindow>("Sectors");

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            Bind();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            Release();
        }

        private void OnProjectChange()
        {
            Release();
            Bind();
        }

        private void OnUndo()
        {
            pages = null;
            Repaint();
        }

        private void OnGUI()
        {
            if (runObject == null)
            {
                EditorGUILayout.HelpBox("The game host has no sector run.", MessageType.Info);
                return;
            }
            runObject.Update();
            fixturesObject?.Update();
            // The run inspector can add or remove rows under an open window.
            var count = runObject.FindProperty("sectors").arraySize +
                        (fixturesObject?.FindProperty("fixtures").arraySize ?? 0);
            if (pages != null && pages.List.Count != count) pages = null;
            pages ??= SectorPages.Load();
            if (pages == null || pages.List.Count == 0)
            {
                EditorGUILayout.HelpBox("The run has no sectors.", MessageType.Info);
                return;
            }
            if (selected >= pages.List.Count) selected = pages.List.Count - 1;
            if (selected < 0) selected = 0;
            var page = pages.List[selected];
            DrawStrip();
            var picked = selected;
            using (new EditorGUILayout.HorizontalScope())
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(200));
                var names = new string[pages.List.Count];
                for (var i = 0; i < names.Length; i++) names[i] = pages.List[i].Name;
                picked = GUILayout.SelectionGrid(selected, names, 1, EditorStyles.toolbarButton);
                EditorGUILayout.EndScrollView();
                pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
                DrawPage(page);
                EditorGUILayout.EndScrollView();
            }
            selected = picked;
            runObject.ApplyModifiedProperties();
            fixturesObject?.ApplyModifiedProperties();
        }

        private void DrawStrip()
        {
            var sectors = runObject.FindProperty("sectors");
            if (sectors == null) return;
            var phaseCount = 0;
            for (var i = 0; i < sectors.arraySize; i++)
            {
                var count = sectors.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(SectorEntry.phases)).arraySize;
                if (count > phaseCount) phaseCount = count;
            }
            var line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            stripScroll = EditorGUILayout.BeginScrollView(stripScroll, GUILayout.Height((6 + phaseCount) * line + 16f));
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70f;
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                    for (var i = 0; i < sectors.arraySize; i++)
                        DrawStripColumn(sectors.GetArrayElementAtIndex(i), i,
                            Mathf.Max(210f, (position.width - 30f) / sectors.arraySize - 4f));
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawStripColumn(SerializedProperty row, int index, float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField($"Sector {index + 1}", EditorStyles.boldLabel);
                DrawStripField(row, nameof(SectorEntry.faction), "Faction");
                DrawStripField(row, nameof(SectorEntry.elite), "Elite");
                DrawStripField(row, nameof(SectorEntry.patrols), "Patrols");
                DrawStripField(row, nameof(SectorEntry.armories), "Armories");
                DrawStripField(row, nameof(SectorEntry.field), "Field");
                var phases = row.FindPropertyRelative(nameof(SectorEntry.phases));
                for (var i = 0; i < phases.arraySize; i++)
                    DrawStripField(phases.GetArrayElementAtIndex(i), nameof(SectorPhase.cap), $"Cap {i + 1}");
            }
        }

        private static void DrawStripField(SerializedProperty parent, string name, string label) =>
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));

        private void DrawPage(SectorPages.Page page)
        {
            if (page.Fixture)
            {
                DrawFixture(page);
                DrawFailures(page);
            }
            else
            {
                using (new EditorGUI.DisabledScope(!PreviewControlPanel.CanFlyCombat()))
                    if (GUILayout.Button("Fly this sector"))
                        PreviewControlPanel.FlyCombat(page.Index);
                DrawFailures(page);
                var row = runObject.FindProperty("sectors").GetArrayElementAtIndex(page.Index);
                DrawRow(row);
                DrawWaves(row);
            }
            usesOpen = EditorGUILayout.Foldout(usesOpen, "Uses (shared)", true);
            if (!usesOpen) return;
            foreach (var value in pages.Uses(page))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"{value.Title} · used by {value.UsedBy}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                    foreach (var part in value.Parts)
                        DrawPart(part);
            }
        }

        private void DrawFailures(SectorPages.Page page)
        {
            foreach (var message in pages.Checks(page))
                EditorGUILayout.HelpBox(message, MessageType.Error);
        }

        private void DrawFixture(SectorPages.Page page)
        {
            if (fixturesObject == null) return;
            var fixtures = fixturesObject.FindProperty("fixtures");
            if (fixtures == null || page.Index >= fixtures.arraySize) return;
            EditorGUILayout.PropertyField(fixtures.GetArrayElementAtIndex(page.Index), GUIContent.none, true);
        }

        private static void DrawRow(SerializedProperty row)
        {
            EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.faction)));
            EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.elite)));
            EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.patrols)));
            EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.armories)));
            EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.field)));
            var random = row.FindPropertyRelative(nameof(SectorEntry.random));
            random.boolValue = EditorGUILayout.Toggle(new GUIContent("Random", random.tooltip), random.boolValue);
            if (!random.boolValue)
                EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.locale)));
        }

        private void DrawWaves(SerializedProperty row)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);
            var phases = row.FindPropertyRelative(nameof(SectorEntry.phases));
            for (var i = 0; i < phases.arraySize; i++)
            {
                var phase = phases.GetArrayElementAtIndex(i);
                EditorGUILayout.LabelField($"Phase {i + 1}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var name in Timing)
                        EditorGUILayout.PropertyField(phase.FindPropertyRelative(name));
                    var groups = phase.FindPropertyRelative(nameof(SectorPhase.groups));
                    if (unfolded.Add(groups.propertyPath)) Unfold(groups);
                    EditorGUILayout.PropertyField(groups, true);
                }
            }
        }

        // Opens each group down to its ships once, so a page shows the roster without clicks and still folds.
        private static void Unfold(SerializedProperty groups)
        {
            groups.isExpanded = true;
            var end = groups.GetEndProperty();
            var child = groups.Copy();
            while (child.NextVisible(true) && !SerializedProperty.EqualContents(child, end))
                child.isExpanded = true;
        }

        private void DrawPart(SectorPages.Part part)
        {
            if (!part.Target) return;
            var serialized = EditorFor(part.Target);
            serialized.Update();
            if (part.Properties == null)
            {
                var property = serialized.GetIterator();
                for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                    if (property.name != "m_Script")
                        EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                foreach (var name in part.Properties)
                {
                    var property = serialized.FindProperty(name);
                    if (property != null) EditorGUILayout.PropertyField(property, true);
                }
            }
            serialized.ApplyModifiedProperties();
        }

        private SerializedObject EditorFor(Object target)
        {
            var id = target.GetInstanceID();
            if (!editors.TryGetValue(id, out var serialized) || serialized.targetObject != target)
            {
                serialized?.Dispose();
                editors[id] = serialized = new SerializedObject(target);
            }
            return serialized;
        }

        private void Bind()
        {
            var host = AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath);
            if (host && host.sessionProfile != null && host.sessionProfile.run)
                runObject = new SerializedObject(host.sessionProfile.run);
            var fixtures = AssetDatabase.LoadAssetAtPath<PreviewFixtures>(PreviewHostBootstrap.FixturesPath);
            if (fixtures) fixturesObject = new SerializedObject(fixtures);
        }

        private void Release()
        {
            runObject?.Dispose();
            runObject = null;
            fixturesObject?.Dispose();
            fixturesObject = null;
            foreach (var serialized in editors.Values) serialized.Dispose();
            editors.Clear();
            pages = null;
        }
    }
}

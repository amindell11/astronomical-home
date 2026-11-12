using System.Collections.Generic;
using Asteroids.Fields;
using Game.Preview.Fixtures;
using Ships;
using Ships.Command;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.Preview.Sectors
{
    internal sealed class SectorsWindow : EditorWindow
    {
        private const float LabelWidth = 72f;
        private const float PartLabelWidth = 170f;
        private const float ObjectFieldWidth = 260f;
        private const float NumberFieldWidth = 60f;
        private const float PageWidth = 720f;
        private const float ColumnWidth = LabelWidth + ObjectFieldWidth + 18f;
        private const float Gap = 4f;
        private const float DragHandle = 20f;
        private const float ShipListWidth = DragHandle + ObjectFieldWidth * 2f + 8f + 12f;
        private const float FixtureTabWidth = 180f;
        private const float FixtureWidth = PartLabelWidth + ObjectFieldWidth + 24f;
        private const int PreviewLimit = 32;

        private readonly Dictionary<int, SerializedObject> editors = new();
        [SerializeField] private int sectorIndex;
        [SerializeField] private int fixtureIndex;
        [SerializeField] private bool fixturesTab;
        private string openGroup;
        private string openUse;
        private ReorderableList groupList;
        private GUIStyle lineButton;
        private SectorPages pages;
        private SerializedObject runObject;
        private SerializedObject fixturesObject;
        private Vector2 stripScroll;
        private Vector2 pageScroll;

        [MenuItem("Window/Sectors")]
        public static void Open() => GetWindow<SectorsWindow>("Sectors");

        private GUIStyle LineButton => lineButton ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Clip,
            richText = false,
            stretchWidth = true,
        };

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
            groupList = null;
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
            var sectors = runObject.FindProperty("sectors");
            var fixtureCountOnAsset = fixturesObject?.FindProperty("fixtures")?.arraySize ?? 0;
            // The run inspector can add or remove rows under an open window.
            var count = (sectors?.arraySize ?? 0) + fixtureCountOnAsset;
            if (pages != null && pages.List.Count != count)
            {
                pages = null;
                openGroup = null;
                openUse = null;
                groupList = null;
            }
            pages ??= SectorPages.Load();
            if (pages == null || pages.List.Count == 0)
            {
                EditorGUILayout.HelpBox("The run has no sectors.", MessageType.Info);
                return;
            }
            var sectorCount = Mathf.Min(sectors?.arraySize ?? 0, pages.List.Count);
            var fixtureCount = pages.List.Count - sectorCount;
            ClampSelection(sectorCount, fixtureCount);
            DrawStrip(sectors, sectorCount);
            GUILayout.Space(6);
            pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(PageWidth)))
            {
                if (fixturesTab)
                {
                    DrawFixturePicker(sectorCount, fixtureCount);
                    if (fixtureCount > 0)
                        DrawPage(pages.List[sectorCount + fixtureIndex]);
                }
                else
                    DrawPage(pages.List[sectorIndex]);
            }
            EditorGUILayout.EndScrollView();
            runObject.ApplyModifiedProperties();
            fixturesObject?.ApplyModifiedProperties();
        }

        private void ClampSelection(int sectorCount, int fixtureCount)
        {
            if (sectorCount <= 0)
            {
                sectorIndex = 0;
                fixturesTab = fixtureCount > 0;
            }
            else if (sectorIndex < 0 || sectorIndex >= sectorCount)
                sectorIndex = Mathf.Clamp(sectorIndex, 0, sectorCount - 1);
            if (fixtureCount <= 0)
                fixtureIndex = 0;
            else if (fixtureIndex < 0 || fixtureIndex >= fixtureCount)
                fixtureIndex = Mathf.Clamp(fixtureIndex, 0, fixtureCount - 1);
        }

        private void DrawStrip(SerializedProperty sectors, int sectorCount)
        {
            var line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            stripScroll = EditorGUILayout.BeginScrollView(stripScroll, GUILayout.Height(line * 7f + 24f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (sectors != null)
                    for (var i = 0; i < sectorCount; i++)
                    {
                        if (i > 0) GUILayout.Space(Gap);
                        DrawStripColumn(sectors.GetArrayElementAtIndex(i), i);
                    }
                GUILayout.Space(Gap);
                DrawFixturesTab();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawStripColumn(SerializedProperty row, int index)
        {
            var picked = !fixturesTab && sectorIndex == index;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox,
                       GUILayout.Width(ColumnWidth), GUILayout.ExpandWidth(false)))
            {
                EditorGUILayout.LabelField($"Sector {index + 1}", EditorStyles.boldLabel);
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = LabelWidth;
                try
                {
                    DrawStripField(row, nameof(SectorEntry.faction), "Faction", ObjectFieldWidth);
                    DrawStripField(row, nameof(SectorEntry.elite), "Elite", ObjectFieldWidth);
                    DrawStripField(row, nameof(SectorEntry.patrols), "Patrols", NumberFieldWidth);
                    DrawStripField(row, nameof(SectorEntry.armories), "Armories", NumberFieldWidth);
                    DrawStripField(row, nameof(SectorEntry.field), "Field", ObjectFieldWidth);
                    DrawCaps(row.FindPropertyRelative(nameof(SectorEntry.phases)));
                }
                finally
                {
                    EditorGUIUtility.labelWidth = labelWidth;
                }
            }
            var rect = GUILayoutUtility.GetLastRect();
            if (picked && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.55f, 0.9f, 0.12f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), new Color(0.35f, 0.62f, 1f, 1f));
            }
            var click = Event.current;
            // A field uses the click, so a mouse-down left over is the column.
            if (click.type != EventType.MouseDown || click.button != 0 || !rect.Contains(click.mousePosition))
                return;
            sectorIndex = index;
            fixturesTab = false;
            GUIUtility.keyboardControl = 0;
            click.Use();
        }

        private static void DrawStripField(SerializedProperty parent, string name, string label, float fieldWidth)
        {
            var property = parent.FindPropertyRelative(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip),
                GUILayout.Width(LabelWidth + fieldWidth), GUILayout.ExpandWidth(false));
        }

        private static void DrawCaps(SerializedProperty phases)
        {
            var width = ColumnWidth - 12f;
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            EditorGUI.LabelField(Take(ref row, LabelWidth), "caps");
            if (phases == null || !phases.isArray) return;
            for (var i = 0; i < phases.arraySize; i++)
            {
                if (row.width < 28f) break;
                if (i > 0) EditorGUI.LabelField(Take(ref row, 10f), "/");
                var cap = phases.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(SectorPhase.cap));
                EditorGUI.PropertyField(Take(ref row, Mathf.Min(40f, row.width)), cap, GUIContent.none);
            }
        }

        private void DrawFixturesTab()
        {
            var tint = GUI.backgroundColor;
            if (fixturesTab) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
            try
            {
                var height = EditorGUIUtility.singleLineHeight + 6f;
                if (GUILayout.Button("Fixtures", EditorStyles.toolbarButton,
                        GUILayout.Width(88), GUILayout.Height(height), GUILayout.ExpandWidth(false)))
                    fixturesTab = true;
            }
            finally
            {
                GUI.backgroundColor = tint;
            }
        }

        private void DrawFixturePicker(int sectorCount, int fixtureCount)
        {
            if (fixtureCount <= 0)
            {
                EditorGUILayout.HelpBox("No fixtures.", MessageType.Info);
                return;
            }
            var names = new string[fixtureCount];
            for (var i = 0; i < fixtureCount; i++) names[i] = pages.List[sectorCount + i].Name;
            var columns = Mathf.Clamp(Mathf.FloorToInt(PageWidth / FixtureTabWidth), 1, fixtureCount);
            fixtureIndex = GUILayout.SelectionGrid(fixtureIndex, names, columns, EditorStyles.toolbarButton,
                GUILayout.MaxWidth(PageWidth));
            GUILayout.Space(6);
        }

        private void DrawPage(SectorPages.Page page)
        {
            DrawFailures(page);
            if (page.Fixture)
                DrawFixture(page);
            else
            {
                DrawLocaleLine(page);
                DrawWaves(runObject.FindProperty("sectors").GetArrayElementAtIndex(page.Index));
            }
            DrawUses(page);
        }

        private void DrawFailures(SectorPages.Page page)
        {
            foreach (var message in pages.Checks(page))
                EditorGUILayout.HelpBox(message, MessageType.Error);
        }

        private void DrawLocaleLine(SectorPages.Page page)
        {
            var row = runObject.FindProperty("sectors").GetArrayElementAtIndex(page.Index);
            var random = row.FindPropertyRelative(nameof(SectorEntry.random));
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (random.boolValue)
                        random.boolValue = EditorGUILayout.ToggleLeft("Random", true,
                            GUILayout.Width(84), GUILayout.ExpandWidth(false));
                    else
                    {
                        EditorGUILayout.PropertyField(row.FindPropertyRelative(nameof(SectorEntry.locale)),
                            GUILayout.Width(LabelWidth + ObjectFieldWidth), GUILayout.ExpandWidth(false));
                        random.boolValue = EditorGUILayout.ToggleLeft("Random", false,
                            GUILayout.Width(84), GUILayout.ExpandWidth(false));
                    }
                    GUILayout.Space(8);
                    using (new EditorGUI.DisabledScope(!PreviewControlPanel.CanFlyCombat()))
                        if (GUILayout.Button("Fly this sector", GUILayout.Width(124), GUILayout.ExpandWidth(false)))
                            PreviewControlPanel.FlyCombat(page.Index);
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        private void DrawWaves(SerializedProperty row)
        {
            Heading("Waves");
            var phases = row.FindPropertyRelative(nameof(SectorEntry.phases));
            for (var i = 0; i < phases.arraySize; i++)
            {
                if (i > 0) GUILayout.Space(8);
                var phase = phases.GetArrayElementAtIndex(i);
                DrawPhaseHeader(i + 1, phase);
                DrawGroups(phase.FindPropertyRelative(nameof(SectorPhase.groups)));
            }
        }

        private static void DrawPhaseHeader(int number, SerializedProperty phase)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(PageWidth));
            EditorGUI.LabelField(Take(ref row, 70f), $"Phase {number}", EditorStyles.boldLabel);
            Dot(ref row);
            Number(ref row, phase, nameof(SectorPhase.seconds));
            EditorGUI.LabelField(Take(ref row, 14f), "s");
            Dot(ref row);
            EditorGUI.LabelField(Take(ref row, 28f), "cap");
            Number(ref row, phase, nameof(SectorPhase.cap));
            Dot(ref row);
            EditorGUI.LabelField(Take(ref row, 58f), "ends at");
            Number(ref row, phase, nameof(SectorPhase.endsAt));
            Dot(ref row);
            EditorGUI.LabelField(Take(ref row, 66f), "breather");
            Number(ref row, phase, nameof(SectorPhase.breather));
            Dot(ref row);
            EditorGUI.LabelField(Take(ref row, 34f), "wait");
            Number(ref row, phase, nameof(SectorPhase.longestWait));
        }

        private static void Number(ref Rect row, SerializedProperty phase, string name) =>
            EditorGUI.PropertyField(Take(ref row, NumberFieldWidth), phase.FindPropertyRelative(name), GUIContent.none);

        private static void Dot(ref Rect row) => EditorGUI.LabelField(Take(ref row, 12f), "\u00b7");

        private void DrawGroups(SerializedProperty groups)
        {
            if (groups == null) return;
            var removed = false;
            for (var i = 0; i < groups.arraySize && !removed; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                var path = group.propertyPath;
                var open = openGroup == path;
                if (GroupLine(SectorPages.GroupSummary(ReadGroup(group)), open))
                {
                    openGroup = open ? null : path;
                    groupList = null;
                }
                if (openGroup == path)
                    removed = DrawGroupEditor(groups, i);
            }
            if (!removed) DrawAddGroup(groups);
        }

        private bool GroupLine(string summary, bool open)
        {
            var marker = open ? "\u25be" : "\u203a";
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                return GUILayout.Button($"{summary} {marker}", LineButton,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private bool DrawGroupEditor(SerializedProperty groups, int index)
        {
            var ships = groups.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(WaveGroup.ships));
            if (ships == null) return false;
            EnsureGroupList(ships);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ShipListWidth)))
                    groupList.DoLayoutList();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                if (!GUILayout.Button("remove group", EditorStyles.miniButton,
                        GUILayout.Width(104), GUILayout.ExpandWidth(false)))
                    return false;
            }
            groups.DeleteArrayElementAtIndex(index);
            openGroup = null;
            groupList = null;
            return true;
        }

        private void DrawAddGroup(SerializedProperty groups)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                if (!GUILayout.Button("+ group", EditorStyles.miniButton,
                        GUILayout.Width(72), GUILayout.ExpandWidth(false)))
                    return;
            }
            var index = groups.arraySize;
            groups.arraySize++;
            var ships = groups.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(WaveGroup.ships));
            if (ships != null) ships.arraySize = 0;
            openGroup = groups.GetArrayElementAtIndex(index).propertyPath;
            groupList = null;
        }

        private void EnsureGroupList(SerializedProperty ships)
        {
            if (groupList != null && groupList.serializedProperty != null &&
                groupList.serializedProperty.propertyPath == ships.propertyPath)
                return;
            groupList = new ReorderableList(ships.serializedObject, ships, true, true, true, true)
            {
                headerHeight = EditorGUIUtility.singleLineHeight + 2f,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f,
                drawHeaderCallback = DrawShipHeader,
                drawElementCallback = DrawShipRow,
            };
        }

        private void DrawShipHeader(Rect rect)
        {
            var x = rect.x + DragHandle;
            var height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(x, rect.y, ObjectFieldWidth, height), "Template");
            EditorGUI.LabelField(new Rect(x + ObjectFieldWidth + 8f, rect.y, ObjectFieldWidth, height), "Pilot");
        }

        private void DrawShipRow(Rect rect, int index, bool active, bool focused)
        {
            var ships = groupList.serializedProperty;
            if (ships == null || index < 0 || index >= ships.arraySize) return;
            var entry = ships.GetArrayElementAtIndex(index);
            var height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, ObjectFieldWidth, height),
                entry.FindPropertyRelative(nameof(WaveDirector.RosterEntry.template)), GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + ObjectFieldWidth + 8f, rect.y, ObjectFieldWidth, height),
                entry.FindPropertyRelative(nameof(WaveDirector.RosterEntry.pilot)), GUIContent.none);
        }

        private static WaveGroup ReadGroup(SerializedProperty group)
        {
            var ships = group.FindPropertyRelative(nameof(WaveGroup.ships));
            if (ships == null || !ships.isArray || ships.arraySize == 0) return default;
            var entries = new WaveDirector.RosterEntry[ships.arraySize];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = ships.GetArrayElementAtIndex(i);
                entries[i] = new WaveDirector.RosterEntry
                {
                    template = entry.FindPropertyRelative(nameof(WaveDirector.RosterEntry.template))?.objectReferenceValue as Ship,
                    pilot = entry.FindPropertyRelative(nameof(WaveDirector.RosterEntry.pilot))?.objectReferenceValue as Commander,
                };
            }
            return new WaveGroup { ships = entries };
        }

        private void DrawUses(SectorPages.Page page)
        {
            var uses = pages.Uses(page);
            if (uses.Count == 0) return;
            Heading("Uses");
            for (var i = 0; i < uses.Count; i++)
            {
                var value = uses[i];
                var key = $"{(page.Fixture ? "f" : "s")}{page.Index}:{i}:{value.Title}";
                var open = openUse == key;
                if (GUILayout.Button(UseLine(value, open), LineButton, GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                    openUse = open ? null : key;
                if (openUse != key) continue;
                using (new EditorGUI.IndentLevelScope())
                    foreach (var part in value.Parts)
                        DrawPart(part);
                GUILayout.Space(6);
            }
        }

        private string UseLine(SectorPages.SharedValue value, bool open)
        {
            var marker = open ? "\u25be" : "\u203a";
            var whole = value.Parts.Count > 0 && value.Parts[0].Properties == null && value.Target;
            var detail = whole ? Kind(value.Target) : ValuePreview(value);
            var head = string.IsNullOrEmpty(detail) ? value.Title : $"{value.Title} ({detail})";
            var used = string.IsNullOrEmpty(value.UsedBy) ? "" : $" \u00b7 used by {value.UsedBy}";
            return $"{head}{used} {marker}";
        }

        private static string Kind(Object target)
        {
            if (target is Faction) return "faction";
            if (target is EliteProfile) return "elite";
            if (target is AsteroidFieldSettings) return "field";
            if (target.GetType().Name.Contains("Feel")) return "feel";
            return ObjectNames.NicifyVariableName(target.GetType().Name).ToLowerInvariant();
        }

        private string ValuePreview(SectorPages.SharedValue value)
        {
            var bits = new List<string>();
            foreach (var part in value.Parts)
            {
                if (!part.Target || part.Properties == null) return null;
                var serialized = EditorFor(part.Target);
                serialized.Update();
                foreach (var name in part.Properties)
                {
                    var property = serialized.FindProperty(name);
                    if (property == null) continue;
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                            bits.Add(property.intValue.ToString());
                            break;
                        case SerializedPropertyType.Float:
                            bits.Add(property.floatValue.ToString("0.##"));
                            break;
                        default:
                            return null;
                    }
                }
            }
            if (bits.Count == 0) return null;
            var text = string.Join(" / ", bits);
            return text.Length <= PreviewLimit ? text : null;
        }

        private void DrawFixture(SectorPages.Page page)
        {
            if (fixturesObject == null) return;
            var fixtures = fixturesObject.FindProperty("fixtures");
            if (fixtures == null || page.Index >= fixtures.arraySize) return;
            EditorGUILayout.PropertyField(fixtures.GetArrayElementAtIndex(page.Index), GUIContent.none, true,
                GUILayout.Width(FixtureWidth));
        }

        private void DrawPart(SectorPages.Part part)
        {
            if (!part.Target) return;
            var serialized = EditorFor(part.Target);
            serialized.Update();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = PartLabelWidth;
            try
            {
                if (part.Properties == null)
                {
                    var property = serialized.GetIterator();
                    for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                        if (property.name != "m_Script")
                            DrawFixed(property);
                }
                else
                {
                    foreach (var name in part.Properties)
                    {
                        var property = serialized.FindProperty(name);
                        if (property != null) DrawFixed(property);
                    }
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
            serialized.ApplyModifiedProperties();
        }

        private static void DrawFixed(SerializedProperty property)
        {
            var field = IsNumber(property) ? NumberFieldWidth : ObjectFieldWidth;
            EditorGUILayout.PropertyField(property, true, GUILayout.Width(PartLabelWidth + field));
        }

        private static bool IsNumber(SerializedProperty property) =>
            property.propertyType is SerializedPropertyType.Integer or SerializedPropertyType.Float;

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

        private static void Heading(string text)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private static Rect Take(ref Rect row, float width)
        {
            var taken = new Rect(row.x, row.y, Mathf.Max(0f, width), row.height);
            var step = width + Gap;
            row.x += step;
            row.width -= step;
            return taken;
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
            groupList = null;
            openGroup = null;
            openUse = null;
            lineButton = null;
        }
    }
}
